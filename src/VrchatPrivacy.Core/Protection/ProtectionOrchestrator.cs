using VrchatPrivacy.Core.Auth;
using VrchatPrivacy.Core.Logging;
using VrchatPrivacy.Core.Logs;
using VrchatPrivacy.Core.Settings;
using VrchatPrivacy.Core.Worlds;

namespace VrchatPrivacy.Core.Protection;

public sealed class ProtectionAppliedEventArgs : EventArgs
{
    public required string WorldId { get; init; }
    public required ProtectionStatus Status { get; init; }
    public string? RepresentGroupId { get; init; }
    public required string Summary { get; init; }
}

public sealed class ProtectionOrchestrator : IDisposable
{
    private readonly IVrchatLogWatcher _watcher;
    private readonly IVrchatSession _session;
    private readonly AppSettingsStore _settingsStore;
    private readonly object _gate = new();
    private AppSettings _settings;
    private bool _paused;
    private ProtectionAppState _state = ProtectionAppState.AwaitingLogin;
    private string? _lastMessage = "Starting…";
    private WorldId? _lastAppliedWorld;
    private ProtectionStatus? _lastAppliedStatus;
    private string? _lastAppliedRepresentGroupId;
    private int _busy;
    private WorldJoinEvent? _pendingJoin;
    private CancellationTokenSource? _applyCts;

    public ProtectionOrchestrator(
        IVrchatLogWatcher watcher,
        IVrchatSession session,
        AppSettingsStore settingsStore)
    {
        _watcher = watcher;
        _session = session;
        _settingsStore = settingsStore;
        _settings = _settingsStore.Load();
        _paused = _settings.StartPaused;
    }

    public event EventHandler? StateChanged;
    public event EventHandler<ProtectionAppliedEventArgs>? ProtectionApplied;

    public ProtectionAppState State
    {
        get { lock (_gate) return _state; }
    }

    public bool IsPaused
    {
        get { lock (_gate) return _paused; }
    }

    public string LastMessage
    {
        get { lock (_gate) return _lastMessage ?? string.Empty; }
    }

    public AppSettings Settings
    {
        get { lock (_gate) return _settings; }
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        SetState(ProtectionAppState.AwaitingLogin, "Checking saved session…");
        var resumed = await _session.TryResumeFromStoreAsync(cancellationToken).ConfigureAwait(false);
        if (resumed)
        {
            SetState(_paused ? ProtectionAppState.Paused : ProtectionAppState.Working, "Session restored.");
        }
        else
        {
            SetState(ProtectionAppState.AwaitingLogin, "Login required.");
        }

        _watcher.WorldJoined += OnWorldJoined;
        await _watcher.StartAsync(cancellationToken).ConfigureAwait(false);

        var current = await _watcher.GetCurrentWorldAsync(cancellationToken).ConfigureAwait(false);
        if (current is not null)
        {
            EnqueueJoin(current);
            await DrainJoinsAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        _watcher.WorldJoined -= OnWorldJoined;
        await _watcher.StopAsync(cancellationToken).ConfigureAwait(false);
    }

    public void SetPaused(bool paused)
    {
        lock (_gate)
        {
            _paused = paused;
        }

        if (paused)
        {
            SetState(ProtectionAppState.Paused, "Protection paused.");
        }
        else if (_session.IsAuthenticated)
        {
            SetState(ProtectionAppState.Working, "Protection resumed.");
        }
        else
        {
            SetState(ProtectionAppState.AwaitingLogin, "Login required.");
        }
    }

    public void ReloadSettings()
    {
        lock (_gate)
        {
            _settings = _settingsStore.Load();
        }

        SetState(State, "Settings reloaded.");
    }

    public void Dispose()
    {
        _watcher.WorldJoined -= OnWorldJoined;
        lock (_gate)
        {
            _applyCts?.Cancel();
            _applyCts?.Dispose();
            _applyCts = null;
        }

        if (_watcher is IDisposable d)
        {
            d.Dispose();
        }
    }

    private void OnWorldJoined(object? sender, WorldJoinEvent e)
    {
        AppLog.Info($"WorldJoined: {e.WorldId.Value}");
        EnqueueJoin(e);
        _ = DrainJoinsAsync(CancellationToken.None);
    }

    private void EnqueueJoin(WorldJoinEvent join)
    {
        lock (_gate)
        {
            // Coalesce: only the latest world matters while we are rate-limited.
            _pendingJoin = join;
            // Abort in-flight rate-limit wait / stale apply so we do not burn a
            // mutation on a world the user already left (Home Blue → sleep Orange).
            try
            {
                _applyCts?.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // ignored
            }
        }
    }

    private bool HasPendingJoin()
    {
        lock (_gate)
        {
            return _pendingJoin is not null;
        }
    }

    private async Task DrainJoinsAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _busy, 1) == 1)
        {
            return;
        }

        try
        {
            while (true)
            {
                WorldJoinEvent? join;
                lock (_gate)
                {
                    join = _pendingJoin;
                    _pendingJoin = null;
                }

                if (join is null)
                {
                    break;
                }

                await HandleJoinCoreAsync(join, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            Interlocked.Exchange(ref _busy, 0);

            // A join may have landed between the last dequeue and clearing busy.
            bool again;
            lock (_gate)
            {
                again = _pendingJoin is not null;
            }

            if (again)
            {
                _ = DrainJoinsAsync(CancellationToken.None);
            }
        }
    }

    private async Task HandleJoinCoreAsync(WorldJoinEvent join, CancellationToken cancellationToken)
    {
        bool paused;
        AppSettings settings;
        lock (_gate)
        {
            paused = _paused;
            settings = _settings;
        }

        var decision = ProtectionDecisionEngine.Decide(settings.ToRules(), paused, join);
        AppLog.Info($"Decision: apply={decision.ShouldApply} world={decision.WorldId?.Value} status={decision.TargetStatus} reason={decision.Reason}");

        if (!decision.ShouldApply || decision.TargetStatus is null || decision.WorldId is null)
        {
            SetState(paused ? ProtectionAppState.Paused : ProtectionAppState.Working, decision.Reason);
            return;
        }

        if (!_session.IsAuthenticated)
        {
            SetState(ProtectionAppState.AwaitingLogin, "Protected world detected; login required.");
            return;
        }

        var representGroupId = settings.TryGetRepresentGroupId(decision.WorldId.Value.Value);

        if (_lastAppliedWorld == decision.WorldId &&
            _lastAppliedStatus == decision.TargetStatus &&
            string.Equals(_lastAppliedRepresentGroupId, representGroupId, StringComparison.OrdinalIgnoreCase))
        {
            SetState(ProtectionAppState.Working, "Already protected for this world.");
            return;
        }

        if (HasPendingJoin())
        {
            AppLog.Info($"Skipping stale apply for {decision.WorldId.Value.Value}; newer join pending.");
            SetState(ProtectionAppState.Working, "Superseded by newer world join.");
            return;
        }

        SetState(ProtectionAppState.Working, $"Applying {ProtectionStatusMapping.ToApiStatus(decision.TargetStatus.Value)}…");

        CancellationTokenSource applyCts;
        lock (_gate)
        {
            _applyCts?.Dispose();
            _applyCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            applyCts = _applyCts;
        }

        try
        {
            var statusChanged = _lastAppliedWorld != decision.WorldId || _lastAppliedStatus != decision.TargetStatus;
            if (statusChanged)
            {
                await _session.ApplyProtectionStatusAsync(decision.TargetStatus.Value, applyCts.Token)
                    .ConfigureAwait(false);
                if (HasPendingJoin())
                {
                    AppLog.Info($"Aborting after status for {decision.WorldId.Value.Value}; newer join pending.");
                    SetState(ProtectionAppState.Working, "Superseded by newer world join.");
                    return;
                }

                _lastAppliedStatus = decision.TargetStatus;
            }

            var groupChanged = !string.IsNullOrWhiteSpace(representGroupId) &&
                !string.Equals(_lastAppliedRepresentGroupId, representGroupId, StringComparison.OrdinalIgnoreCase);
            if (groupChanged)
            {
                SetState(ProtectionAppState.Working, $"Representing group {representGroupId}…");
                await _session.SetRepresentingGroupAsync(representGroupId!, applyCts.Token).ConfigureAwait(false);
                _lastAppliedRepresentGroupId = representGroupId;
            }

            _lastAppliedWorld = decision.WorldId;
            var label = ProtectionStatusMapping.ToUserLabel(decision.TargetStatus.Value);
            var msg = $"Applied {label} for {decision.WorldId.Value.Value}";
            if (!string.IsNullOrWhiteSpace(representGroupId))
            {
                msg += " (+ group)";
            }

            SetState(ProtectionAppState.Working, msg + ".");

            if (statusChanged || groupChanged)
            {
                ProtectionApplied?.Invoke(this, new ProtectionAppliedEventArgs
                {
                    WorldId = decision.WorldId.Value.Value,
                    Status = decision.TargetStatus.Value,
                    RepresentGroupId = representGroupId,
                    Summary = msg,
                });
            }
        }
        catch (OperationCanceledException) when (HasPendingJoin() || applyCts.IsCancellationRequested)
        {
            AppLog.Info($"Apply cancelled for {decision.WorldId.Value.Value} (superseded).");
            SetState(ProtectionAppState.Working, "Superseded by newer world join.");
        }
        catch (Exception ex)
        {
            AppLog.Error("HandleJoin apply failed", ex);
            SetState(ProtectionAppState.Failed, $"Status update failed: {ex.Message}");
        }
    }

    private void SetState(ProtectionAppState state, string message)
    {
        lock (_gate)
        {
            _state = state;
            _lastMessage = message;
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}

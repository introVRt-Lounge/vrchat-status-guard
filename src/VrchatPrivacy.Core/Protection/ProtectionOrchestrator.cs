using VrchatPrivacy.Core.Auth;
using VrchatPrivacy.Core.Logs;
using VrchatPrivacy.Core.Settings;
using VrchatPrivacy.Core.Worlds;

namespace VrchatPrivacy.Core.Protection;

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
            await HandleJoinAsync(current, cancellationToken).ConfigureAwait(false);
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
        if (_watcher is IDisposable d)
        {
            d.Dispose();
        }
    }

    private void OnWorldJoined(object? sender, WorldJoinEvent e)
    {
        _ = HandleJoinAsync(e, CancellationToken.None);
    }

    private async Task HandleJoinAsync(WorldJoinEvent join, CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _busy, 1) == 1)
        {
            return;
        }

        try
        {
            bool paused;
            AppSettings settings;
            lock (_gate)
            {
                paused = _paused;
                settings = _settings;
            }

            var decision = ProtectionDecisionEngine.Decide(settings.ToRules(), paused, join);
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

            // Skip duplicate apply for same world+status+group (still leave room for rate limit on real changes).
            if (_lastAppliedWorld == decision.WorldId &&
                _lastAppliedStatus == decision.TargetStatus &&
                string.Equals(_lastAppliedRepresentGroupId, representGroupId, StringComparison.OrdinalIgnoreCase))
            {
                SetState(ProtectionAppState.Working, "Already protected for this world.");
                return;
            }

            SetState(ProtectionAppState.Working, $"Applying {ProtectionStatusMapping.ToApiStatus(decision.TargetStatus.Value)}…");
            try
            {
                if (_lastAppliedWorld != decision.WorldId || _lastAppliedStatus != decision.TargetStatus)
                {
                    await _session.ApplyProtectionStatusAsync(decision.TargetStatus.Value, cancellationToken)
                        .ConfigureAwait(false);
                    _lastAppliedStatus = decision.TargetStatus;
                }

                if (!string.IsNullOrWhiteSpace(representGroupId) &&
                    !string.Equals(_lastAppliedRepresentGroupId, representGroupId, StringComparison.OrdinalIgnoreCase))
                {
                    SetState(ProtectionAppState.Working, $"Representing group {representGroupId}…");
                    await _session.SetRepresentingGroupAsync(representGroupId, cancellationToken).ConfigureAwait(false);
                    _lastAppliedRepresentGroupId = representGroupId;
                }

                _lastAppliedWorld = decision.WorldId;
                var msg = $"Applied {ProtectionStatusMapping.ToApiStatus(decision.TargetStatus.Value)} for {decision.WorldId.Value.Value}";
                if (!string.IsNullOrWhiteSpace(representGroupId))
                {
                    msg += $" (group {representGroupId})";
                }

                SetState(ProtectionAppState.Working, msg + ".");
            }
            catch (Exception ex)
            {
                SetState(ProtectionAppState.Failed, $"Status update failed: {ex.Message}");
            }
        }
        finally
        {
            Interlocked.Exchange(ref _busy, 0);
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

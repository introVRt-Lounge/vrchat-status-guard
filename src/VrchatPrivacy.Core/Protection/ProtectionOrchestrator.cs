using VrchatPrivacy.Core.Activity;
using VrchatPrivacy.Core.Auth;
using VrchatPrivacy.Core.Logging;
using VrchatPrivacy.Core.Logs;
using VrchatPrivacy.Core.Settings;
using VrchatPrivacy.Core.Worlds;

namespace VrchatPrivacy.Core.Protection;

public sealed class ProtectionAppliedEventArgs : EventArgs
{
    public required string WorldId { get; init; }
    public string? WorldName { get; init; }
    public required ProtectionStatus Status { get; init; }
    public string? RepresentGroupId { get; init; }
    public string? RepresentGroupName { get; init; }
    public required string Summary { get; init; }

    /// <summary>
    /// True when this was a status mutation verified on the unofficial API.
    /// The running VRChat client may still show a different colour (#62).
    /// </summary>
    public bool ApiStatusVerified { get; init; }

    /// <summary>
    /// True when the game process is running - in-game colour must be treated as unconfirmed.
    /// </summary>
    public bool ClientSyncUncertain { get; init; }
}

public sealed class ProtectionOrchestrator : IDisposable
{
    private readonly IVrchatLogWatcher _watcher;
    private readonly IVrchatSession _session;
    private readonly AppSettingsStore _settingsStore;
    private readonly ActivityJournal _activity;
    private readonly WorldInfoLookup? _worlds;
    private readonly IVrchatClientPresence _presence;
    private readonly bool _ownsPresence;
    private readonly object _gate = new();
    private AppSettings _settings;
    private bool _paused;
    private ProtectionAppState _state = ProtectionAppState.AwaitingLogin;
    private string? _lastMessage = "Starting…";
    private WorldId? _lastAppliedWorld;
    private ProtectionStatus? _lastAppliedStatus;
    private string? _lastAppliedRepresentGroupId;
    private int _busy;
    private int _repBusy;
    private WorldJoinEvent? _pendingJoin;
    private CancellationTokenSource? _applyCts;
    private CancellationTokenSource? _repCts;
    private string? _pendingRepWorldId;
    private string? _pendingRepGroupId;
    private ProtectionStatus? _pendingRepStatus;

    public ProtectionOrchestrator(
        IVrchatLogWatcher watcher,
        IVrchatSession session,
        AppSettingsStore settingsStore,
        ActivityJournal? activity = null,
        WorldInfoLookup? worlds = null,
        IVrchatClientPresence? presence = null)
    {
        _watcher = watcher;
        _session = session;
        _settingsStore = settingsStore;
        _activity = activity ?? new ActivityJournal();
        _worlds = worlds;
        if (presence is null)
        {
            _presence = new VrchatProcessPresence();
            _ownsPresence = true;
        }
        else
        {
            _presence = presence;
            _ownsPresence = false;
        }

        _settings = _settingsStore.Load();
        _paused = _settings.StartPaused;
    }

    public event EventHandler? StateChanged;
    public event EventHandler<ProtectionAppliedEventArgs>? ProtectionApplied;

    public ActivityJournal Activity => _activity;

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

        _presence.Changed += OnClientPresenceChanged;
        _presence.Start();

        _watcher.WorldJoined += OnWorldJoined;
        await _watcher.StartAsync(cancellationToken).ConfigureAwait(false);

        if (!_presence.IsRunning)
        {
            HaltMutations("Client not running - mutations idle.");
            return;
        }

        await CatchUpCurrentWorldAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        _presence.Changed -= OnClientPresenceChanged;
        _presence.Stop();
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
            HaltMutations(_session.IsAuthenticated ? "Protection paused." : "Login required.");
            if (_session.IsAuthenticated)
            {
                _activity.RecordPaused();
                SetState(ProtectionAppState.Paused, "Protection paused.");
            }
            else
            {
                SetState(ProtectionAppState.AwaitingLogin, "Login required.");
            }

            return;
        }

        _activity.RecordResumed();
        if (!_session.IsAuthenticated)
        {
            SetState(ProtectionAppState.AwaitingLogin, "Login required.");
            return;
        }

        if (!_presence.IsRunning)
        {
            SetState(ProtectionAppState.Disconnected, "Client not running - mutations idle.");
            return;
        }

        SetState(ProtectionAppState.Working, "Protection resumed.");
        _ = CatchUpCurrentWorldAsync(CancellationToken.None);
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
        _presence.Changed -= OnClientPresenceChanged;
        _presence.Stop();
        if (_ownsPresence)
        {
            _presence.Dispose();
        }

        _watcher.WorldJoined -= OnWorldJoined;
        lock (_gate)
        {
            CancelAndDispose(ref _applyCts);
            CancelAndDispose(ref _repCts);
            _pendingJoin = null;
            _pendingRepWorldId = null;
            _pendingRepGroupId = null;
            _pendingRepStatus = null;
        }

        if (_watcher is IDisposable d)
        {
            d.Dispose();
        }
    }

    private void OnClientPresenceChanged(object? sender, bool running)
    {
        if (!running)
        {
            HaltMutations("Client not running - mutations idle.");
            return;
        }

        bool paused;
        lock (_gate)
        {
            paused = _paused;
        }

        if (paused || !_session.IsAuthenticated)
        {
            return;
        }

        SetState(ProtectionAppState.Working, "Client running - catching up.");
        _ = CatchUpCurrentWorldAsync(CancellationToken.None);
    }

    /// <summary>Cancel in-flight status/repping and drop queues (#59).</summary>
    private void HaltMutations(string reason)
    {
        lock (_gate)
        {
            _pendingJoin = null;
            CancelPendingRep_NoLock();
            TryCancel(_applyCts);
            TryCancel(_repCts);
        }

        AppLog.Info($"Halt mutations: {reason}");
        if (!_paused && _session.IsAuthenticated)
        {
            SetState(ProtectionAppState.Disconnected, reason);
        }
    }

    private bool ClientAllowsMutations()
    {
        if (_presence.IsRunning)
        {
            return true;
        }

        HaltMutations("Client not running - mutations idle.");
        return false;
    }

    private async Task CatchUpCurrentWorldAsync(CancellationToken cancellationToken)
    {
        if (!ClientAllowsMutations())
        {
            return;
        }

        var current = await _watcher.GetCurrentWorldAsync(cancellationToken).ConfigureAwait(false);
        if (current is null)
        {
            return;
        }

        if (!ClientAllowsMutations())
        {
            return;
        }

        AppLog.Info($"CatchUp: {current.WorldId.Value}");
        EnqueueJoin(current);
        await DrainJoinsAsync(cancellationToken).ConfigureAwait(false);
    }

    private void OnWorldJoined(object? sender, WorldJoinEvent e)
    {
        if (!_presence.IsRunning)
        {
            AppLog.Info($"Ignoring WorldJoined {e.WorldId.Value}; client not running.");
            HaltMutations("Client not running - mutations idle.");
            return;
        }

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
            // Status wins: abort in-flight status wait and any deferred repping.
            TryCancel(_applyCts);
            CancelPendingRep_NoLock();
        }
    }

    private void CancelPendingRep_NoLock()
    {
        _pendingRepWorldId = null;
        _pendingRepGroupId = null;
        _pendingRepStatus = null;
        TryCancel(_repCts);
    }

    private static void TryCancel(CancellationTokenSource? cts)
    {
        if (cts is null)
        {
            return;
        }

        try
        {
            cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // ignored
        }
    }

    private static void CancelAndDispose(ref CancellationTokenSource? cts)
    {
        TryCancel(cts);
        cts?.Dispose();
        cts = null;
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
                if (!ClientAllowsMutations())
                {
                    lock (_gate)
                    {
                        _pendingJoin = null;
                    }

                    break;
                }

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

            if (again && _presence.IsRunning)
            {
                _ = DrainJoinsAsync(CancellationToken.None);
            }
        }
    }

    private async Task HandleJoinCoreAsync(WorldJoinEvent join, CancellationToken cancellationToken)
    {
        if (!ClientAllowsMutations())
        {
            return;
        }

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

        var worldId = decision.WorldId.Value.Value;
        var representGroupId = settings.TryGetRepresentGroupId(worldId);
        var statusChanged = _lastAppliedWorld != decision.WorldId || _lastAppliedStatus != decision.TargetStatus;
        var groupChanged = !string.IsNullOrWhiteSpace(representGroupId) &&
            !string.Equals(_lastAppliedRepresentGroupId, representGroupId, StringComparison.OrdinalIgnoreCase);

        if (!statusChanged && !groupChanged)
        {
            SetState(ProtectionAppState.Working, "Already protected for this world.");
            return;
        }

        if (HasPendingJoin())
        {
            AppLog.Info($"Skipping stale apply for {worldId}; newer join pending.");
            SetState(ProtectionAppState.Working, "Superseded by newer world join.");
            return;
        }

        if (!ClientAllowsMutations())
        {
            return;
        }

        CancellationTokenSource applyCts;
        lock (_gate)
        {
            _applyCts?.Dispose();
            _applyCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            applyCts = _applyCts;
        }

        try
        {
            var worldInfo = TryWorldInfo(worldId);
            if (statusChanged || groupChanged)
            {
                _activity.RecordProtectedWorldEntered(
                    worldId,
                    worldInfo?.Name,
                    worldInfo?.ThumbnailImageUrl,
                    decision.TargetStatus.Value);
            }

            // Status is the privacy-critical path. Notify as soon as it lands; repping is deferred.
            if (statusChanged)
            {
                if (!ClientAllowsMutations())
                {
                    return;
                }

                SetState(ProtectionAppState.Working, $"Applying {ProtectionStatusMapping.ToApiStatus(decision.TargetStatus.Value)}…");
                await _session.ApplyProtectionStatusAsync(decision.TargetStatus.Value, applyCts.Token)
                    .ConfigureAwait(false);
                if (HasPendingJoin())
                {
                    AppLog.Info($"Aborting after status for {worldId}; newer join pending.");
                    SetState(ProtectionAppState.Working, "Superseded by newer world join.");
                    return;
                }

                if (!ClientAllowsMutations())
                {
                    AppLog.Info($"Aborting after status for {worldId}; client exited.");
                    return;
                }

                _lastAppliedWorld = decision.WorldId;
                _lastAppliedStatus = decision.TargetStatus;

                var label = ProtectionStatusMapping.ToUserLabel(decision.TargetStatus.Value);
                var worldLabel = string.IsNullOrWhiteSpace(worldInfo?.Name) ? worldId : worldInfo!.Name;
                var msg = $"Applied {label} for {worldLabel}";
                SetState(ProtectionAppState.Working, msg + ".");
                _activity.RecordStatusApplied(
                    worldId,
                    worldInfo?.Name,
                    worldInfo?.ThumbnailImageUrl,
                    decision.TargetStatus.Value);
                ProtectionApplied?.Invoke(this, new ProtectionAppliedEventArgs
                {
                    WorldId = worldId,
                    WorldName = worldInfo?.Name,
                    Status = decision.TargetStatus.Value,
                    RepresentGroupId = null,
                    Summary = msg,
                    ApiStatusVerified = true,
                    ClientSyncUncertain = _presence.IsRunning,
                });
            }
            else
            {
                _lastAppliedWorld = decision.WorldId;
                _lastAppliedStatus = decision.TargetStatus;
            }

            if (groupChanged && ClientAllowsMutations())
            {
                QueueRepping(worldId, representGroupId!, decision.TargetStatus.Value);
            }
        }
        catch (OperationCanceledException) when (HasPendingJoin() || applyCts.IsCancellationRequested)
        {
            AppLog.Info($"Apply cancelled for {worldId}.");
            if (!_presence.IsRunning)
            {
                SetState(ProtectionAppState.Disconnected, "Client not running - mutations idle.");
            }
            else
            {
                SetState(ProtectionAppState.Working, "Superseded by newer world join.");
            }
        }
        catch (Exception ex)
        {
            AppLog.Error("HandleJoin apply failed", ex);
            SetState(ProtectionAppState.Failed, $"Status update failed: {ex.Message}");
        }
    }

    private void QueueRepping(string worldId, string groupId, ProtectionStatus status)
    {
        if (!_presence.IsRunning)
        {
            AppLog.Info($"Not queueing repping {groupId}; client not running.");
            return;
        }

        lock (_gate)
        {
            _pendingRepWorldId = worldId;
            _pendingRepGroupId = groupId;
            _pendingRepStatus = status;
            try
            {
                _repCts?.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // ignored
            }
        }

        _ = DrainReppingAsync();
    }

    private async Task DrainReppingAsync()
    {
        if (Interlocked.Exchange(ref _repBusy, 1) == 1)
        {
            return;
        }

        try
        {
            while (true)
            {
                if (!ClientAllowsMutations())
                {
                    break;
                }

                string? worldId;
                string? groupId;
                ProtectionStatus? status;
                CancellationTokenSource repCts;
                lock (_gate)
                {
                    worldId = _pendingRepWorldId;
                    groupId = _pendingRepGroupId;
                    status = _pendingRepStatus;
                    _pendingRepWorldId = null;
                    _pendingRepGroupId = null;
                    _pendingRepStatus = null;
                    if (worldId is null || groupId is null || status is null)
                    {
                        break;
                    }

                    _repCts?.Dispose();
                    _repCts = new CancellationTokenSource();
                    repCts = _repCts;
                }

                if (HasPendingJoin())
                {
                    AppLog.Info($"Skipping repping {groupId}; status join pending.");
                    break;
                }

                if (string.Equals(_lastAppliedRepresentGroupId, groupId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!ClientAllowsMutations())
                {
                    break;
                }

                var groupName = await TryGroupNameAsync(groupId, repCts.Token).ConfigureAwait(false);
                var groupLabel = string.IsNullOrWhiteSpace(groupName) ? groupId : groupName!;
                SetState(ProtectionAppState.Working, $"Representing {groupLabel}…");
                try
                {
                    await _session.SetRepresentingGroupAsync(groupId, repCts.Token).ConfigureAwait(false);
                    if (HasPendingJoin() || repCts.IsCancellationRequested || !_presence.IsRunning)
                    {
                        AppLog.Info($"Repping aborted for {groupId}.");
                        break;
                    }

                    _lastAppliedRepresentGroupId = groupId;
                    var worldInfo = TryWorldInfo(worldId);
                    var worldLabel = string.IsNullOrWhiteSpace(worldInfo?.Name) ? worldId : worldInfo!.Name;
                    var msg = $"Repping {groupLabel} in {worldLabel}";
                    SetState(ProtectionAppState.Working, msg + ".");
                    _activity.RecordGroupApplied(
                        worldId,
                        worldInfo?.Name,
                        status.Value,
                        groupId,
                        groupName);
                    ProtectionApplied?.Invoke(this, new ProtectionAppliedEventArgs
                    {
                        WorldId = worldId,
                        WorldName = worldInfo?.Name,
                        Status = status.Value,
                        RepresentGroupId = groupId,
                        RepresentGroupName = groupName,
                        Summary = msg,
                    });
                }
                catch (OperationCanceledException)
                {
                    AppLog.Info($"Repping cancelled for {groupId}.");
                    break;
                }
                catch (Exception ex)
                {
                    AppLog.Error("Repping apply failed", ex);
                    SetState(ProtectionAppState.Failed, $"Group update failed: {ex.Message}");
                    break;
                }
            }
        }
        finally
        {
            Interlocked.Exchange(ref _repBusy, 0);

            bool again;
            lock (_gate)
            {
                again = _pendingRepGroupId is not null;
            }

            if (again && !HasPendingJoin() && _presence.IsRunning)
            {
                _ = DrainReppingAsync();
            }
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

    private WorldInfo? TryWorldInfo(string worldId) =>
        _worlds?.TryGetCached(worldId);

    private async Task<string?> TryGroupNameAsync(string groupId, CancellationToken cancellationToken)
    {
        try
        {
            var groups = await _session.GetUserGroupsAsync(cancellationToken).ConfigureAwait(false);
            foreach (var g in groups)
            {
                if (string.Equals(g.Id, groupId, StringComparison.OrdinalIgnoreCase))
                {
                    return string.IsNullOrWhiteSpace(g.Name) ? g.HeaderLabel : g.Name;
                }
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn($"TryGroupName: failed for {groupId}: {ex.Message}");
        }

        return null;
    }
}

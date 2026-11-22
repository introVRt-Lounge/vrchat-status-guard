using VrchatPrivacy.Core.Activity;
using VrchatPrivacy.Core.Auth;
using VrchatPrivacy.Core.Groups;
using VrchatPrivacy.Core.Logs;
using VrchatPrivacy.Core.Protection;
using VrchatPrivacy.Core.Settings;
using VrchatPrivacy.Core.Worlds;

namespace VrchatStatusGuard.Core.Tests;

[Collection("AppLogSerial")]
public class ProtectionOrchestratorTests : IDisposable
{
    private const string HomeId = AppSettings.DefaultProtectedWorldId;
    private const string SleepId = "wrld_aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee";
    private readonly string _logDir;
    private readonly ActivityJournal _activity;

    public ProtectionOrchestratorTests()
    {
        _logDir = Path.Combine(Path.GetTempPath(), "vsg-orch-log-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_logDir);
        VrchatPrivacy.Core.Logging.AppLog.SetDirectoryOverride(_logDir);
        _activity = new ActivityJournal();
        _activity.SetDirectoryOverride(_logDir);
    }

    public void Dispose()
    {
        VrchatPrivacy.Core.Logging.AppLog.SetDirectoryOverride(null);
        _activity.SetDirectoryOverride(null);
        try
        {
            Directory.Delete(_logDir, recursive: true);
        }
        catch
        {
            // best-effort
        }
    }

    private ProtectionOrchestrator CreateOrch(
        IVrchatLogWatcher watcher,
        IVrchatSession session,
        AppSettingsStore store,
        IVrchatClientPresence? presence = null) =>
        new(watcher, session, store, _activity, presence: presence ?? new AlwaysOnPresence());

    [Fact]
    public async Task ConcurrentJoinWhileBusy_AppliesLatestWorld()
    {
        var home = Parse(HomeId);
        var sleep = Parse(SleepId);
        var dir = NewTempDir();
        var store = new AppSettingsStore(dir);
        var settings = AppSettings.CreateDefault();
        settings.ProtectedWorldIds = new List<string> { HomeId, SleepId };
        settings.StatusByWorld[HomeId] = ProtectionStatus.Blue;
        settings.ProtectionStatus = ProtectionStatus.Orange;
        settings.StartPaused = false;
        store.Save(settings);

        var applyStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var blockFirst = 0;
        var session = new FakeSession
        {
            OnApply = async (_, ct) =>
            {
                if (Interlocked.Exchange(ref blockFirst, 1) == 0)
                {
                    applyStarted.TrySetResult();
                    // Block until cancelled by a newer WorldJoined (supersede).
                    await Task.Delay(TimeSpan.FromSeconds(30), ct);
                }
            },
        };
        var watcher = new FakeWatcher();
        using var orch = CreateOrch(watcher, session, store);
        var applied = new List<ProtectionAppliedEventArgs>();
        orch.ProtectionApplied += (_, e) => applied.Add(e);

        await orch.StartAsync();

        watcher.RaiseJoin(new WorldJoinEvent(home, "1", DateTimeOffset.UtcNow));
        await applyStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Sleep join arrives while Home apply is still in flight (rate-limit / slow API).
        watcher.RaiseJoin(new WorldJoinEvent(sleep, "2", DateTimeOffset.UtcNow));

        await WaitUntilAsync(
            () => session.AppliedStatuses.Count == 1 &&
                  session.AppliedStatuses[0] == ProtectionStatus.Orange,
            TimeSpan.FromSeconds(5));

        // Stale Home Blue must not be applied - only sleep Orange.
        Assert.Equal(new[] { ProtectionStatus.Orange }, session.AppliedStatuses);
        Assert.Contains(applied, e => e.WorldId == SleepId && e.Status == ProtectionStatus.Orange);
        Assert.DoesNotContain(applied, e => e.WorldId == HomeId);
    }

    [Fact]
    public async Task MultipleJoinsWhileBusy_CoalescesToLatestOnly()
    {
        var home = Parse(HomeId);
        var mid = Parse("wrld_11111111-2222-4333-8444-555555555555");
        var sleep = Parse(SleepId);
        var dir = NewTempDir();
        var store = new AppSettingsStore(dir);
        var settings = AppSettings.CreateDefault();
        settings.ProtectedWorldIds = new List<string> { HomeId, mid.Value, SleepId };
        settings.StatusByWorld[HomeId] = ProtectionStatus.Blue;
        settings.StatusByWorld[mid.Value] = ProtectionStatus.Red;
        settings.ProtectionStatus = ProtectionStatus.Orange;
        store.Save(settings);

        var applyStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var blockFirst = 0;
        var session = new FakeSession
        {
            OnApply = async (_, ct) =>
            {
                if (Interlocked.Exchange(ref blockFirst, 1) == 0)
                {
                    applyStarted.TrySetResult();
                    await Task.Delay(TimeSpan.FromSeconds(30), ct);
                }
            },
        };
        var watcher = new FakeWatcher();
        using var orch = CreateOrch(watcher, session, store);
        await orch.StartAsync();

        watcher.RaiseJoin(new WorldJoinEvent(home, "1", DateTimeOffset.UtcNow));
        await applyStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        watcher.RaiseJoin(new WorldJoinEvent(mid, "2", DateTimeOffset.UtcNow));
        watcher.RaiseJoin(new WorldJoinEvent(sleep, "3", DateTimeOffset.UtcNow));

        await WaitUntilAsync(
            () => session.AppliedStatuses.Count == 1 &&
                  session.AppliedStatuses[0] == ProtectionStatus.Orange,
            TimeSpan.FromSeconds(5));

        // Home cancelled; mid coalesced away; only latest (sleep) applied.
        Assert.Equal(new[] { ProtectionStatus.Orange }, session.AppliedStatuses);
    }

    [Fact]
    public async Task SuccessfulApply_RaisesProtectionApplied()
    {
        var sleep = Parse(SleepId);
        var dir = NewTempDir();
        var store = new AppSettingsStore(dir);
        var settings = AppSettings.CreateDefault();
        settings.ProtectedWorldIds = new List<string> { SleepId };
        settings.ProtectionStatus = ProtectionStatus.Orange;
        settings.RepresentGroupByWorld[SleepId] = "grp_test";
        store.Save(settings);

        var session = new FakeSession();
        var watcher = new FakeWatcher();
        using var orch = CreateOrch(watcher, session, store);
        var applied = new List<ProtectionAppliedEventArgs>();
        orch.ProtectionApplied += (_, e) => applied.Add(e);

        await orch.StartAsync();
        watcher.RaiseJoin(new WorldJoinEvent(sleep, "1", DateTimeOffset.UtcNow));

        await WaitUntilAsync(
            () => session.AppliedStatuses.Count == 1 && session.RepresentedGroups.Count == 1,
            TimeSpan.FromSeconds(5));

        Assert.Contains(applied, e => e.WorldId == SleepId && e.Status == ProtectionStatus.Orange);
        Assert.Equal(new[] { ProtectionStatus.Orange }, session.AppliedStatuses);
        Assert.Equal(new[] { "grp_test" }, session.RepresentedGroups);
    }

    [Fact]
    public async Task StatusNotify_FiresBeforeReppingCompletes()
    {
        var sleep = Parse(SleepId);
        var dir = NewTempDir();
        var store = new AppSettingsStore(dir);
        var settings = AppSettings.CreateDefault();
        settings.ProtectedWorldIds = new List<string> { SleepId };
        settings.ProtectionStatus = ProtectionStatus.Orange;
        settings.RepresentGroupByWorld[SleepId] = "grp_slow";
        store.Save(settings);

        var repStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseRep = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var session = new FakeSession
        {
            OnSetGroup = async (_, ct) =>
            {
                repStarted.TrySetResult();
                await releaseRep.Task.WaitAsync(ct);
            },
        };
        var watcher = new FakeWatcher();
        using var orch = CreateOrch(watcher, session, store);
        ProtectionAppliedEventArgs? first = null;
        orch.ProtectionApplied += (_, e) => first ??= e;

        await orch.StartAsync();
        watcher.RaiseJoin(new WorldJoinEvent(sleep, "1", DateTimeOffset.UtcNow));

        await WaitUntilAsync(() => first is not null, TimeSpan.FromSeconds(5));
        Assert.Equal(ProtectionStatus.Orange, first!.Status);
        Assert.Equal(new[] { ProtectionStatus.Orange }, session.AppliedStatuses);
        Assert.Empty(session.RepresentedGroups);

        await repStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        releaseRep.TrySetResult();
        await WaitUntilAsync(() => session.RepresentedGroups.Count == 1, TimeSpan.FromSeconds(5));
        Assert.Equal(new[] { "grp_slow" }, session.RepresentedGroups);
    }

    [Fact]
    public async Task NewJoinDuringRepWait_CancelsRepAndAppliesNewStatus()
    {
        var home = Parse(HomeId);
        var sleep = Parse(SleepId);
        var dir = NewTempDir();
        var store = new AppSettingsStore(dir);
        var settings = AppSettings.CreateDefault();
        settings.ProtectedWorldIds = new List<string> { HomeId, SleepId };
        settings.StatusByWorld[HomeId] = ProtectionStatus.Blue;
        settings.ProtectionStatus = ProtectionStatus.Orange;
        settings.RepresentGroupByWorld[HomeId] = "grp_home";
        store.Save(settings);

        var repStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var session = new FakeSession
        {
            OnSetGroup = async (_, ct) =>
            {
                repStarted.TrySetResult();
                await Task.Delay(TimeSpan.FromSeconds(30), ct);
            },
        };
        var watcher = new FakeWatcher();
        using var orch = CreateOrch(watcher, session, store);

        await orch.StartAsync();
        watcher.RaiseJoin(new WorldJoinEvent(home, "1", DateTimeOffset.UtcNow));
        await repStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        watcher.RaiseJoin(new WorldJoinEvent(sleep, "2", DateTimeOffset.UtcNow));

        await WaitUntilAsync(
            () => session.AppliedStatuses.Count >= 2 &&
                  session.AppliedStatuses[^1] == ProtectionStatus.Orange,
            TimeSpan.FromSeconds(5));

        Assert.Equal(ProtectionStatus.Blue, session.AppliedStatuses[0]);
        Assert.Equal(ProtectionStatus.Orange, session.AppliedStatuses[^1]);
        Assert.Empty(session.RepresentedGroups);
    }

    [Fact]
    public async Task ClientExit_CancelsDeferredRepping()
    {
        var home = Parse(HomeId);
        var dir = NewTempDir();
        var store = new AppSettingsStore(dir);
        var settings = AppSettings.CreateDefault();
        settings.ProtectedWorldIds = new List<string> { HomeId };
        settings.StatusByWorld[HomeId] = ProtectionStatus.Blue;
        settings.RepresentGroupByWorld[HomeId] = "grp_481c4787-5fca-44b2-b6e4-d2d8c585f125";
        settings.StartPaused = false;
        store.Save(settings);

        var repStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var session = new FakeSession
        {
            OnSetGroup = async (_, ct) =>
            {
                repStarted.TrySetResult();
                await Task.Delay(TimeSpan.FromSeconds(30), ct);
            },
        };
        var presence = new TogglePresence(running: true);
        var watcher = new FakeWatcher();
        using var orch = CreateOrch(watcher, session, store, presence);
        await orch.StartAsync();

        watcher.RaiseJoin(new WorldJoinEvent(home, "1", DateTimeOffset.UtcNow));
        await WaitUntilAsync(() => session.AppliedStatuses.Count == 1, TimeSpan.FromSeconds(5));
        await repStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        presence.SetRunning(false);

        await WaitUntilAsync(
            () => orch.State == ProtectionAppState.Disconnected && session.RepresentedGroups.Count == 0,
            TimeSpan.FromSeconds(5));

        Assert.Equal(ProtectionAppState.Disconnected, orch.State);
        Assert.Empty(session.RepresentedGroups);
        Assert.Contains("not running", orch.LastMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReppingStatusMessage_UsesGroupNameNotId()
    {
        var home = Parse(HomeId);
        var dir = NewTempDir();
        var store = new AppSettingsStore(dir);
        var settings = AppSettings.CreateDefault();
        settings.ProtectedWorldIds = new List<string> { HomeId };
        settings.StatusByWorld[HomeId] = ProtectionStatus.Orange;
        settings.RepresentGroupByWorld[HomeId] = "grp_481c4787-5fca-44b2-b6e4-d2d8c585f125";
        settings.StartPaused = false;
        store.Save(settings);

        var session = new FakeSession
        {
            Groups =
            [
                new GroupInfo(
                    "grp_481c4787-5fca-44b2-b6e4-d2d8c585f125",
                    "introVRt Lounge",
                    "INTRO",
                    "1234",
                    null,
                    null,
                    false),
            ],
        };
        var watcher = new FakeWatcher();
        using var orch = CreateOrch(watcher, session, store);
        await orch.StartAsync();
        watcher.RaiseJoin(new WorldJoinEvent(home, "1", DateTimeOffset.UtcNow));

        await WaitUntilAsync(
            () => session.RepresentedGroups.Count == 1 &&
                  orch.LastMessage.Contains("introVRt Lounge", StringComparison.Ordinal),
            TimeSpan.FromSeconds(5));

        Assert.DoesNotContain("grp_", orch.LastMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("introVRt Lounge", orch.LastMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StatusApplyWhileClientRunning_StaysWorkingWithoutManualConfirm()
    {
        var sleep = Parse(SleepId);
        var dir = NewTempDir();
        var store = new AppSettingsStore(dir);
        var settings = AppSettings.CreateDefault();
        settings.ProtectedWorldIds = new List<string> { SleepId };
        settings.ProtectionStatus = ProtectionStatus.Orange;
        settings.StartPaused = false;
        store.Save(settings);

        var session = new FakeSession();
        var watcher = new FakeWatcher();
        using var orch = CreateOrch(watcher, session, store);
        ProtectionAppliedEventArgs? applied = null;
        orch.ProtectionApplied += (_, e) => applied = e;

        await orch.StartAsync();
        watcher.RaiseJoin(new WorldJoinEvent(sleep, "1", DateTimeOffset.UtcNow));

        await WaitUntilAsync(
            () => orch.State == ProtectionAppState.Working && applied is not null,
            TimeSpan.FromSeconds(5));

        Assert.Equal(ProtectionStatus.Orange, session.AppliedStatuses.Single());
        Assert.True(applied!.ApiStatusVerified);
        Assert.True(applied.ClientSyncUncertain);
        Assert.Contains("Applied", orch.LastMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CONFIRM", orch.LastMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ClientNotRunning_IgnoresWorldJoined()
    {
        var home = Parse(HomeId);
        var dir = NewTempDir();
        var store = new AppSettingsStore(dir);
        var settings = AppSettings.CreateDefault();
        settings.ProtectedWorldIds = new List<string> { HomeId };
        settings.StatusByWorld[HomeId] = ProtectionStatus.Blue;
        settings.StartPaused = false;
        store.Save(settings);

        var session = new FakeSession();
        var presence = new TogglePresence(running: false);
        var watcher = new FakeWatcher();
        using var orch = CreateOrch(watcher, session, store, presence);
        await orch.StartAsync();

        watcher.RaiseJoin(new WorldJoinEvent(home, "1", DateTimeOffset.UtcNow));
        await Task.Delay(200);

        Assert.Empty(session.AppliedStatuses);
        Assert.Equal(ProtectionAppState.Disconnected, orch.State);
    }

    private static WorldId Parse(string id)
    {
        if (!WorldId.TryParse(id, out var world))
        {
            throw new ArgumentException($"Invalid world id fixture: {id}");
        }

        return world;
    }

    private static string NewTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "vsg-orch-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static async Task WaitUntilAsync(Func<bool> predicate, TimeSpan timeout)
    {
        var start = DateTime.UtcNow;
        while (!predicate())
        {
            if (DateTime.UtcNow - start > timeout)
            {
                throw new TimeoutException("Condition not met within timeout.");
            }

            await Task.Delay(20).ConfigureAwait(false);
        }
    }

    private sealed class FakeWatcher : IVrchatLogWatcher
    {
        public event EventHandler<WorldJoinEvent>? WorldJoined;

        public Task<WorldJoinEvent?> GetCurrentWorldAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<WorldJoinEvent?>(null);

        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public void RaiseJoin(WorldJoinEvent join) => WorldJoined?.Invoke(this, join);
    }

    private sealed class AlwaysOnPresence : IVrchatClientPresence
    {
        public bool IsRunning => true;
#pragma warning disable CS0067 // tests never flip presence
        public event EventHandler<bool>? Changed;
#pragma warning restore CS0067
        public void Start() { }
        public void Stop() { }
        public void Dispose() { }
    }

    private sealed class TogglePresence : IVrchatClientPresence
    {
        private bool _running;

        public TogglePresence(bool running) => _running = running;

        public bool IsRunning => _running;

        public event EventHandler<bool>? Changed;

        public void Start() { }

        public void Stop() { }

        public void Dispose() { }

        public void SetRunning(bool running)
        {
            if (_running == running)
            {
                return;
            }

            _running = running;
            Changed?.Invoke(this, running);
        }
    }

    private sealed class FakeSession : IVrchatSession
    {
        public bool IsAuthenticated => true;
        public List<ProtectionStatus> AppliedStatuses { get; } = new();
        public List<string> RepresentedGroups { get; } = new();
        public List<GroupInfo> Groups { get; init; } = new();
        public Func<ProtectionStatus, CancellationToken, Task>? OnApply { get; set; }
        public Func<string, CancellationToken, Task>? OnSetGroup { get; set; }

        public Task<LoginChallenge?> BeginLoginAsync(string username, string password, CancellationToken cancellationToken = default) =>
            Task.FromResult<LoginChallenge?>(null);

        public Task CompleteTotpAsync(string code, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task CompleteEmailOtpAsync(string code, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<bool> TryResumeFromStoreAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task LogoutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<SessionSnapshot> GetCurrentUserAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new SessionSnapshot("usr_x", "Test", "ask me"));

        public async Task ApplyProtectionStatusAsync(ProtectionStatus status, CancellationToken cancellationToken = default)
        {
            if (OnApply is not null)
            {
                await OnApply(status, cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            AppliedStatuses.Add(status);
        }

        public Task<IReadOnlyList<WorldInfo>> GetRecentWorldsAsync(int count = 20, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<WorldInfo>>(Array.Empty<WorldInfo>());

        public Task<IReadOnlyList<GroupInfo>> GetUserGroupsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<GroupInfo>>(Groups);

        public async Task SetRepresentingGroupAsync(string groupId, CancellationToken cancellationToken = default)
        {
            if (OnSetGroup is not null)
            {
                await OnSetGroup(groupId, cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            RepresentedGroups.Add(groupId);
        }
    }
}

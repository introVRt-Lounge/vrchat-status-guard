namespace VrchatPrivacy.Core.Logs;

/// <summary>
/// Watches %UserProfile%\AppData\LocalLow\VRChat\VRChat\output_log_*.txt,
/// following newly created files when VRChat restarts.
/// </summary>
public interface IVrchatLogWatcher
{
    event EventHandler<WorldJoinEvent>? WorldJoined;

    /// <summary>
    /// Scans the newest log file for the latest join (startup mid-session case).
    /// </summary>
    Task<WorldJoinEvent?> GetCurrentWorldAsync(CancellationToken cancellationToken = default);

    Task StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);
}

using StatusGuard.Core.Worlds;

namespace StatusGuard.Core.Logs;

/// <summary>
/// Parsed world-entry event from a VRChat output_log line. No player names are captured.
/// </summary>
public sealed record WorldJoinEvent(
    WorldId WorldId,
    string? InstanceToken,
    DateTimeOffset? ObservedAt);

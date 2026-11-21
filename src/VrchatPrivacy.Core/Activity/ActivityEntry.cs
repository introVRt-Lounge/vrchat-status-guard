using VrchatPrivacy.Core.Protection;

namespace VrchatPrivacy.Core.Activity;

public enum FeedKind
{
    ProtectedWorldEntered,
    StatusApplied,
    GroupApplied,
    Paused,
    Resumed,
}

/// <summary>User-facing activity row (Basic log). Stable for JSON persistence.</summary>
public sealed record ActivityEntry(
    DateTimeOffset At,
    FeedKind Kind,
    string Title,
    string Detail,
    string? WorldId,
    string? WorldName,
    string? ThumbnailImageUrl,
    ProtectionStatus? Status,
    string? GroupId,
    string? GroupName = null,
    string? GroupIconUrl = null);

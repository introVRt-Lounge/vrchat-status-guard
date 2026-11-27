namespace StatusGuard.Core.Protection;

/// <summary>
/// User-facing protection colours. Maps to VRChat API status strings.
/// Blue (join me) is applied only when explicitly configured for a world —
/// never invented as a "restore" when leaving a protected world.
/// </summary>
public enum ProtectionStatus
{
    /// <summary>Orange - Ask Me. Location hidden; invite requests still arrive.</summary>
    Orange = 0,

    /// <summary>Red - Do Not Disturb / busy. Location hidden; invite notifications suppressed.</summary>
    Red = 1,

    /// <summary>Blue - Join Me. Location visible; intentional per-world opt-in only.</summary>
    Blue = 2,
}

public static class ProtectionStatusMapping
{
    public static string ToApiStatus(ProtectionStatus status) => status switch
    {
        ProtectionStatus.Orange => "ask me",
        ProtectionStatus.Red => "busy",
        ProtectionStatus.Blue => "join me",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };

    public static string ToUserLabel(ProtectionStatus status) => status switch
    {
        ProtectionStatus.Orange => "Orange (ask me)",
        ProtectionStatus.Red => "Red (busy / DND)",
        ProtectionStatus.Blue => "Blue (join me)",
        _ => status.ToString(),
    };

    public static bool TryFromUserLabel(string? label, out ProtectionStatus status)
    {
        status = ProtectionStatus.Orange;
        if (string.IsNullOrWhiteSpace(label))
        {
            return false;
        }

        switch (label.Trim().ToLowerInvariant())
        {
            case "orange":
            case "ask me":
            case "askme":
                status = ProtectionStatus.Orange;
                return true;
            case "red":
            case "busy":
            case "dnd":
            case "do not disturb":
                status = ProtectionStatus.Red;
                return true;
            case "blue":
            case "join me":
            case "joinme":
                status = ProtectionStatus.Blue;
                return true;
            default:
                return false;
        }
    }
}

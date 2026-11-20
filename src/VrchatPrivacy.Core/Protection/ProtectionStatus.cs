namespace VrchatPrivacy.Core.Protection;

/// <summary>
/// User-facing protection colours. Maps to VRChat API status strings.
/// Blue (join me) is never applied automatically.
/// </summary>
public enum ProtectionStatus
{
    /// <summary>Orange - Ask Me. Location hidden; invite requests still arrive.</summary>
    Orange = 0,

    /// <summary>Red - Do Not Disturb / busy. Location hidden; invite notifications suppressed.</summary>
    Red = 1,
}

public static class ProtectionStatusMapping
{
    public static string ToApiStatus(ProtectionStatus status) => status switch
    {
        ProtectionStatus.Orange => "ask me",
        ProtectionStatus.Red => "busy",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
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
            default:
                return false;
        }
    }
}

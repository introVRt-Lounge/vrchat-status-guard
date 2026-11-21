using VrchatPrivacy.Core.Protection;

namespace VrchatPrivacy.App.Ui;

/// <summary>
/// NotifyIcon must not hold live SystemIcons references - Windows can blank them.
/// Clone once per state and reuse.
/// </summary>
internal static class TrayIcons
{
    private static readonly Icon Working = (Icon)SystemIcons.Shield.Clone();
    private static readonly Icon Paused = (Icon)SystemIcons.Warning.Clone();
    private static readonly Icon Failed = (Icon)SystemIcons.Error.Clone();
    private static readonly Icon AwaitingLogin = (Icon)SystemIcons.Question.Clone();
    private static readonly Icon Idle = (Icon)SystemIcons.Application.Clone();

    public static Icon ForState(ProtectionAppState state) => state switch
    {
        ProtectionAppState.Working => Working,
        ProtectionAppState.Paused => Paused,
        ProtectionAppState.Failed => Failed,
        ProtectionAppState.Disconnected => Failed,
        ProtectionAppState.AwaitingLogin => AwaitingLogin,
        _ => Idle,
    };

    public static Icon Default => Working;
}

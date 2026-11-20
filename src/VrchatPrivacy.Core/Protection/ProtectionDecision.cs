using VrchatPrivacy.Core.Logs;
using VrchatPrivacy.Core.Worlds;

namespace VrchatPrivacy.Core.Protection;

public enum ProtectionAppState
{
    Working,
    Paused,
    Disconnected,
    AwaitingLogin,
    Failed,
}

public sealed record ProtectionDecision(
    bool ShouldApply,
    WorldId? WorldId,
    ProtectionStatus? TargetStatus,
    string Reason);

/// <summary>
/// Pure decision helper: given rules + pause + latest join, decide whether to change status.
/// Never proposes restoring blue / join me.
/// </summary>
public static class ProtectionDecisionEngine
{
    public static ProtectionDecision Decide(
        ProtectedWorldRules rules,
        bool isPaused,
        WorldJoinEvent? latestJoin)
    {
        if (isPaused)
        {
            return new ProtectionDecision(false, latestJoin?.WorldId, null, "Protection paused.");
        }

        if (latestJoin is null)
        {
            return new ProtectionDecision(false, null, null, "No current world detected.");
        }

        var target = rules.StatusForWorld(latestJoin.WorldId);
        if (target is null)
        {
            return new ProtectionDecision(
                false,
                latestJoin.WorldId,
                null,
                "Current world is not protected; status left unchanged.");
        }

        return new ProtectionDecision(
            true,
            latestJoin.WorldId,
            target,
            $"Protected world entered; apply {ProtectionStatusMapping.ToApiStatus(target.Value)}.");
    }
}

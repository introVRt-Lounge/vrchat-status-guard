using VrchatPrivacy.Core.Logs;
using VrchatPrivacy.Core.Protection;
using VrchatPrivacy.Core.Worlds;

namespace VrchatPrivacy.Core.Tests;

public class ProtectionDecisionTests
{
    private static WorldId Parse(string id)
    {
        if (!WorldId.TryParse(id, out var world))
        {
            throw new ArgumentException($"Invalid world id fixture: {id}");
        }

        return world;
    }

    [Fact]
    public void EnteringProtectedWorld_AppliesDefaultOrange()
    {
        var sleep = Parse("wrld_7a0c95e2-476c-4655-9b4e-7380933c4fd3");
        var rules = new ProtectedWorldRules(new[] { sleep }, ProtectionStatus.Orange);
        var join = new WorldJoinEvent(sleep, "1", null);

        var decision = ProtectionDecisionEngine.Decide(rules, isPaused: false, join);

        Assert.True(decision.ShouldApply);
        Assert.Equal(ProtectionStatus.Orange, decision.TargetStatus);
        Assert.Equal("ask me", ProtectionStatusMapping.ToApiStatus(decision.TargetStatus!.Value));
    }

    [Fact]
    public void LeavingProtectedWorld_DoesNotRestoreBlue()
    {
        var sleep = Parse("wrld_7a0c95e2-476c-4655-9b4e-7380933c4fd3");
        var home = Parse("wrld_4432ea9b-729c-46e3-8eaf-846aa0a37fdd");
        var rules = new ProtectedWorldRules(new[] { sleep });
        var join = new WorldJoinEvent(home, "1", null);

        var decision = ProtectionDecisionEngine.Decide(rules, isPaused: false, join);

        Assert.False(decision.ShouldApply);
        Assert.Null(decision.TargetStatus);
        Assert.DoesNotContain("join me", decision.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("blue", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Paused_NeverApplies()
    {
        var sleep = Parse("wrld_7a0c95e2-476c-4655-9b4e-7380933c4fd3");
        var rules = new ProtectedWorldRules(new[] { sleep });
        var join = new WorldJoinEvent(sleep, "1", null);

        var decision = ProtectionDecisionEngine.Decide(rules, isPaused: true, join);

        Assert.False(decision.ShouldApply);
    }

    [Fact]
    public void RedMapping_UsesBusyApiStatus()
    {
        Assert.Equal("busy", ProtectionStatusMapping.ToApiStatus(ProtectionStatus.Red));
    }
}

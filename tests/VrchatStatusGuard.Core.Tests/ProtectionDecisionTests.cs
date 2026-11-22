using VrchatPrivacy.Core.Logs;
using VrchatPrivacy.Core.Protection;
using VrchatPrivacy.Core.Settings;
using VrchatPrivacy.Core.Worlds;

namespace VrchatStatusGuard.Core.Tests;

public class ProtectionDecisionTests
{
    // Synthetic non-home world for leave / override contrast (not a real shipped default).
    private const string OtherWorldId = "wrld_11111111-2222-4333-8444-555555555555";

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
        var home = Parse(AppSettings.DefaultProtectedWorldId);
        var rules = new ProtectedWorldRules(new[] { home }, ProtectionStatus.Orange);
        var join = new WorldJoinEvent(home, "1", null);

        var decision = ProtectionDecisionEngine.Decide(rules, isPaused: false, join);

        Assert.True(decision.ShouldApply);
        Assert.Equal(ProtectionStatus.Orange, decision.TargetStatus);
        Assert.Equal("ask me", ProtectionStatusMapping.ToApiStatus(decision.TargetStatus!.Value));
    }

    [Fact]
    public void LeavingProtectedWorld_DoesNotRestoreBlue()
    {
        var home = Parse(AppSettings.DefaultProtectedWorldId);
        var other = Parse(OtherWorldId);
        var rules = new ProtectedWorldRules(new[] { home });
        var join = new WorldJoinEvent(other, "1", null);

        var decision = ProtectionDecisionEngine.Decide(rules, isPaused: false, join);

        Assert.False(decision.ShouldApply);
        Assert.Null(decision.TargetStatus);
        Assert.DoesNotContain("join me", decision.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("blue", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Paused_NeverApplies()
    {
        var home = Parse(AppSettings.DefaultProtectedWorldId);
        var rules = new ProtectedWorldRules(new[] { home });
        var join = new WorldJoinEvent(home, "1", null);

        var decision = ProtectionDecisionEngine.Decide(rules, isPaused: true, join);

        Assert.False(decision.ShouldApply);
    }

    [Fact]
    public void RedMapping_UsesBusyApiStatus()
    {
        Assert.Equal("busy", ProtectionStatusMapping.ToApiStatus(ProtectionStatus.Red));
    }

    [Fact]
    public void BlueMapping_UsesJoinMeApiStatus()
    {
        Assert.Equal("join me", ProtectionStatusMapping.ToApiStatus(ProtectionStatus.Blue));
    }

    [Fact]
    public void PerWorldBlueOverride_AppliesJoinMe()
    {
        var home = Parse(AppSettings.DefaultProtectedWorldId);
        var hangout = Parse(OtherWorldId);
        var overrides = new Dictionary<string, ProtectionStatus>(StringComparer.OrdinalIgnoreCase)
        {
            [hangout.Value] = ProtectionStatus.Blue,
        };
        var rules = new ProtectedWorldRules(
            new[] { home, hangout },
            ProtectionStatus.Orange,
            overrides);

        var decision = ProtectionDecisionEngine.Decide(
            rules,
            isPaused: false,
            new WorldJoinEvent(hangout, "1", null));

        Assert.True(decision.ShouldApply);
        Assert.Equal(ProtectionStatus.Blue, decision.TargetStatus);
        Assert.Equal("join me", ProtectionStatusMapping.ToApiStatus(decision.TargetStatus!.Value));
    }
}

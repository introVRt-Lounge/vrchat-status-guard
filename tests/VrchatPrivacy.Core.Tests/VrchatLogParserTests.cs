using VrchatPrivacy.Core.Logs;
using VrchatPrivacy.Core.Settings;

namespace VrchatPrivacy.Core.Tests;

public class VrchatLogParserTests
{
    // Sanitized fixtures from live Windows PC logs (2026-11-20). Player/user ids truncated.
    private const string HomeJoin =
        "2026.11.20 00:42:39 Debug      -  [Behaviour] Joining wrld_4432ea9b-729c-46e3-8eaf-846aa0a37fdd:28829~friends(usr_REDACTED)~region(eu)";

    private const string OtherJoin =
        "2026.11.20 00:43:18 Debug      -  [Behaviour] Joining wrld_11111111-2222-4333-8444-555555555555:75618~group(grp_REDACTED)~groupAccessType(public)~ageGate~region(us)";

    [Fact]
    public void TryParseWorldJoin_ExtractsWorldIdFromLiveHomeLine()
    {
        Assert.True(VrchatLogParser.TryParseWorldJoin(HomeJoin, out var join));
        Assert.Equal(AppSettings.DefaultProtectedWorldId, join!.WorldId.Value);
        Assert.StartsWith("28829~friends", join.InstanceToken);
    }

    [Fact]
    public void TryParseWorldJoin_ExtractsWorldIdFromOtherWorldLine()
    {
        Assert.True(VrchatLogParser.TryParseWorldJoin(OtherJoin, out var join));
        Assert.Equal("wrld_11111111-2222-4333-8444-555555555555", join!.WorldId.Value);
    }

    [Fact]
    public void TryParseWorldJoin_IgnoresEnteringRoomNameOnly()
    {
        const string nameOnly =
            "2026.11.20 00:43:18 Debug      -  [Behaviour] Entering Room: VRChat Home";
        Assert.False(VrchatLogParser.TryParseWorldJoin(nameOnly, out _));
    }

    [Fact]
    public void FindLatestWorldJoin_ReturnsMostRecent()
    {
        var lines = new[]
        {
            HomeJoin,
            "2026.11.20 00:42:39 Debug      -  [Behaviour] Successfully joined room",
            OtherJoin,
            "2026.11.20 00:43:18 Debug      -  [Behaviour] Successfully joined room",
        };

        var latest = VrchatLogParser.FindLatestWorldJoin(lines);
        Assert.NotNull(latest);
        Assert.Equal("wrld_11111111-2222-4333-8444-555555555555", latest!.WorldId.Value);
    }
}

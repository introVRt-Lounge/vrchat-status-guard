using VrchatPrivacy.Core.Settings;
using VrchatPrivacy.Core.Worlds;

namespace VrchatPrivacy.Core.Tests;

public class WorldIdTests
{
    [Theory]
    [InlineData(AppSettings.DefaultProtectedWorldId)]
    [InlineData("wrld_11111111-2222-4333-8444-555555555555")]
    public void TryParse_AcceptsValidWorldId(string id)
    {
        Assert.True(WorldId.TryParse(id, out var world));
        Assert.Equal(id, world.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-world")]
    [InlineData("wrld_short")]
    [InlineData("usr_aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee")]
    public void TryParse_RejectsInvalid(string id)
    {
        Assert.False(WorldId.TryParse(id, out _));
    }

    [Fact]
    public void TryParseFromUserInput_ExtractsFromUrl()
    {
        const string url =
            "https://vrchat.com/home/world/wrld_4432ea9b-729c-46e3-8eaf-846aa0a37fdd/info";

        Assert.True(WorldId.TryParseFromUserInput(url, out var world));
        Assert.Equal(AppSettings.DefaultProtectedWorldId, world.Value);
    }
}

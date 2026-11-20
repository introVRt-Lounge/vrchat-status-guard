using VrchatPrivacy.Core.Worlds;

namespace VrchatPrivacy.Core.Tests;

public class WorldIdTests
{
    [Theory]
    [InlineData("wrld_7a0c95e2-476c-4655-9b4e-7380933c4fd3")]
    [InlineData("wrld_4432ea9b-729c-46e3-8eaf-846aa0a37fdd")]
    public void TryParse_AcceptsValidWorldId(string id)
    {
        Assert.True(WorldId.TryParse(id, out var world));
        Assert.Equal(id, world.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-world")]
    [InlineData("wrld_short")]
    [InlineData("usr_7a0c95e2-476c-4655-9b4e-7380933c4fd3")]
    public void TryParse_RejectsInvalid(string id)
    {
        Assert.False(WorldId.TryParse(id, out _));
    }

    [Fact]
    public void TryParseFromUserInput_ExtractsFromUrl()
    {
        const string url =
            "https://vrchat.com/home/world/wrld_7a0c95e2-476c-4655-9b4e-7380933c4fd3";

        Assert.True(WorldId.TryParseFromUserInput(url, out var world));
        Assert.Equal("wrld_7a0c95e2-476c-4655-9b4e-7380933c4fd3", world.Value);
    }
}

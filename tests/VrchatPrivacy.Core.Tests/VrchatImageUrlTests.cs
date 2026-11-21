using VrchatPrivacy.Core.Groups;

namespace VrchatPrivacy.Core.Tests;

public class VrchatImageUrlTests
{
    [Fact]
    public void PreferThumbnail_RewritesFileUrlToImage256()
    {
        var input = "https://api.vrchat.cloud/api/1/file/file_ba5c99a7-5ed5-4e36-9db3-dc99f5ce5c32/1/file";
        var expected = "https://api.vrchat.cloud/api/1/image/file_ba5c99a7-5ed5-4e36-9db3-dc99f5ce5c32/1/256";
        Assert.Equal(expected, VrchatImageUrl.PreferThumbnail(input, 256));
    }

    [Fact]
    public void PreferThumbnail_LeavesImageUrlButCanResize()
    {
        var input = "https://api.vrchat.cloud/api/1/image/file_ba5c99a7-5ed5-4e36-9db3-dc99f5ce5c32/1/128";
        var expected = "https://api.vrchat.cloud/api/1/image/file_ba5c99a7-5ed5-4e36-9db3-dc99f5ce5c32/1/256";
        Assert.Equal(expected, VrchatImageUrl.PreferThumbnail(input, 256));
    }

    [Fact]
    public void PreferThumbnail_PassthroughNonVrchat()
    {
        var input = "https://files.vrchat.cloud/thumbnails/world.png";
        Assert.Equal(input, VrchatImageUrl.PreferThumbnail(input, 256));
    }

    [Fact]
    public void PreferThumbnail_NullOrEmpty_ReturnsNull()
    {
        Assert.Null(VrchatImageUrl.PreferThumbnail(null, 256));
        Assert.Null(VrchatImageUrl.PreferThumbnail("  ", 256));
    }
}

using VrchatPrivacy.Core.Groups;
using VrchatPrivacy.Core.Settings;

namespace VrchatStatusGuard.Core.Tests;

public class LocalVrchatGroupsTests
{
    [Fact]
    public void ReadGroupOrder_DoesNotThrow()
    {
        // On CI/Linux this returns empty; on a VRChat Windows box it may return grp_ ids.
        var order = LocalVrchatGroups.ReadGroupOrder();
        Assert.NotNull(order);
        Assert.All(order, id => Assert.StartsWith("grp_", id));
    }

    [Fact]
    public void RoundTrip_PersistsRepresentGroupByWorld()
    {
        var dir = Path.Combine(Path.GetTempPath(), "vrchat-status-guard-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var store = new AppSettingsStore(dir);
            var settings = store.Load();
            settings.SetRepresentGroupId(AppSettings.DefaultProtectedWorldId, "grp_5500ebf7-f940-4775-a5c0-f6a02c2ce618");
            store.Save(settings);

            var reloaded = store.Load();
            Assert.Equal(
                "grp_5500ebf7-f940-4775-a5c0-f6a02c2ce618",
                reloaded.TryGetRepresentGroupId(AppSettings.DefaultProtectedWorldId));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void GroupInfo_HeaderLabel_UsesShortCode()
    {
        var g = new GroupInfo(
            "grp_abc",
            "introVRt Lounge",
            "INTRO",
            "1234",
            null,
            null,
            false);
        Assert.Equal("INTRO.1234", g.HeaderLabel);
    }
}

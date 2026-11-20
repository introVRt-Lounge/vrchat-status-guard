using VrchatPrivacy.Core.Protection;
using VrchatPrivacy.Core.Settings;

namespace VrchatPrivacy.Core.Tests;

public class AppSettingsTests
{
    [Fact]
    public void Default_IncludesCuddleAndSleepWorld()
    {
        var settings = AppSettings.CreateDefault();
        Assert.Contains(AppSettings.DefaultProtectedWorldId, settings.ProtectedWorldIds);
        Assert.Equal(ProtectionStatus.Orange, settings.ProtectionStatus);
    }

    [Fact]
    public void ToRules_ProtectsDefaultWorld()
    {
        var rules = AppSettings.CreateDefault().ToRules();
        Assert.True(VrchatPrivacy.Core.Worlds.WorldId.TryParse(AppSettings.DefaultProtectedWorldId, out var world));
        Assert.Equal(ProtectionStatus.Orange, rules.StatusForWorld(world));
    }

    [Fact]
    public void RoundTrip_PersistsWorldList()
    {
        var dir = Path.Combine(Path.GetTempPath(), "vrchat-privacy-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var store = new AppSettingsStore(dir);
            var settings = store.Load();
            settings.ProtectedWorldIds.Add("wrld_4432ea9b-729c-46e3-8eaf-846aa0a37fdd");
            settings.ProtectionStatus = ProtectionStatus.Red;
            store.Save(settings);

            var reloaded = store.Load();
            Assert.Contains(AppSettings.DefaultProtectedWorldId, reloaded.ProtectedWorldIds);
            Assert.Contains("wrld_4432ea9b-729c-46e3-8eaf-846aa0a37fdd", reloaded.ProtectedWorldIds);
            Assert.Equal(ProtectionStatus.Red, reloaded.ProtectionStatus);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}

using StatusGuard.Core.Protection;
using StatusGuard.Core.Settings;

namespace StatusGuard.Core.Tests;

public class AppSettingsTests
{
    private const string OtherWorldId = "wrld_11111111-2222-4333-8444-555555555555";

    [Fact]
    public void Default_IncludesVrchatHomeWorld()
    {
        var settings = AppSettings.CreateDefault();
        Assert.Contains(AppSettings.DefaultProtectedWorldId, settings.ProtectedWorldIds);
        Assert.Equal("wrld_4432ea9b-729c-46e3-8eaf-846aa0a37fdd", AppSettings.DefaultProtectedWorldId);
        Assert.Equal(ProtectionStatus.Orange, settings.ProtectionStatus);
    }

    [Fact]
    public void ToRules_ProtectsDefaultWorld()
    {
        var rules = AppSettings.CreateDefault().ToRules();
        Assert.True(StatusGuard.Core.Worlds.WorldId.TryParse(AppSettings.DefaultProtectedWorldId, out var world));
        Assert.Equal(ProtectionStatus.Orange, rules.StatusForWorld(world));
    }

    [Fact]
    public void RoundTrip_PersistsWorldList()
    {
        var dir = Path.Combine(Path.GetTempPath(), "vrchat-status-guard-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var store = new AppSettingsStore(dir);
            var settings = store.Load();
            settings.ProtectedWorldIds.Add(OtherWorldId);
            settings.ProtectionStatus = ProtectionStatus.Red;
            store.Save(settings);

            var reloaded = store.Load();
            Assert.Contains(AppSettings.DefaultProtectedWorldId, reloaded.ProtectedWorldIds);
            Assert.Contains(OtherWorldId, reloaded.ProtectedWorldIds);
            Assert.Equal(ProtectionStatus.Red, reloaded.ProtectionStatus);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}

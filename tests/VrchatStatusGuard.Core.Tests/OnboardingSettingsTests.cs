using VrchatPrivacy.Core.Settings;

namespace VrchatStatusGuard.Core.Tests;

public class OnboardingSettingsTests
{
    [Fact]
    public void LegacySettingsWithoutFlag_SkipWizardIfAlreadyUsed()
    {
        var dir = Path.Combine(Path.GetTempPath(), "vrchat-status-guard-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, "settings.json");
            // Pre-wizard shape: no onboardingCompleted key
            File.WriteAllText(path, """
                {
                  "protectedWorldIds": [ "wrld_4432ea9b-729c-46e3-8eaf-846aa0a37fdd" ],
                  "protectionStatus": "Orange",
                  "usernameHint": "HeavyGee"
                }
                """);

            var store = new AppSettingsStore(dir);
            var settings = store.Load();
            Assert.True(settings.OnboardingCompleted);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void FreshSettings_NeedOnboarding()
    {
        var dir = Path.Combine(Path.GetTempPath(), "vrchat-status-guard-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var store = new AppSettingsStore(dir);
            var settings = store.Load();
            Assert.False(settings.OnboardingCompleted);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}

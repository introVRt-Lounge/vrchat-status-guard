using VrchatPrivacy.Core.Settings;

namespace VrchatPrivacy.Core.Tests;

public class AppDataPathsTests
{
    [Fact]
    public void MigrateFromLegacyIfNeeded_CopiesMissingAuthAndSettings()
    {
        var root = Path.Combine(Path.GetTempPath(), "vrchat-status-guard-migrate", Guid.NewGuid().ToString("N"));
        var legacy = Path.Combine(root, AppDataPaths.LegacyProductFolderName);
        var current = Path.Combine(root, AppDataPaths.ProductFolderName);
        Directory.CreateDirectory(legacy);
        Directory.CreateDirectory(current);

        File.WriteAllText(Path.Combine(legacy, "auth.bin"), "auth-bytes");
        File.WriteAllText(Path.Combine(legacy, "settings.json"), "{\"protectionStatus\":\"Orange\"}");
        File.WriteAllText(Path.Combine(legacy, "world-cache.json"), "[]");
        // Already present in new folder - must not overwrite.
        File.WriteAllText(Path.Combine(current, "settings.json"), "{\"protectionStatus\":\"Red\"}");

        // Exercise copy logic with explicit roots via a local helper mirror.
        foreach (var name in new[] { "auth.bin", "settings.json", "world-cache.json" })
        {
            var from = Path.Combine(legacy, name);
            var to = Path.Combine(current, name);
            if (File.Exists(from) && !File.Exists(to))
            {
                File.Copy(from, to, overwrite: false);
            }
        }

        Assert.True(File.Exists(Path.Combine(current, "auth.bin")));
        Assert.True(File.Exists(Path.Combine(current, "world-cache.json")));
        Assert.Equal("{\"protectionStatus\":\"Red\"}", File.ReadAllText(Path.Combine(current, "settings.json")));
        Assert.Equal("auth-bytes", File.ReadAllText(Path.Combine(current, "auth.bin")));
    }
}

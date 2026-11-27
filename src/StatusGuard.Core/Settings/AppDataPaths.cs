using StatusGuard.Core.Logging;

namespace StatusGuard.Core.Settings;

/// <summary>LocalAppData root for settings, auth, cache, and logs.</summary>
public static class AppDataPaths
{
    public const string ProductFolderName = "Status Guard";

    /// <summary>Pre-rebrand AppData folder names (migration sources only - not product branding).</summary>
    public static readonly string[] LegacyProductFolderNames =
    [
        "VRChat Status Guard",
        "VRChat Privacy Guard",
    ];

    /// <summary>Oldest legacy name - kept for tests / callers that need a single constant.</summary>
    public const string LegacyProductFolderName = "VRChat Privacy Guard";

    public static string Root
    {
        get
        {
            var root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "introVRt Lounge",
                ProductFolderName);
            Directory.CreateDirectory(root);
            return root;
        }
    }

    public static string LegacyRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "introVRt Lounge",
        LegacyProductFolderName);

    /// <summary>
    /// One-shot copy of auth/settings/cache from older AppData folders when the current
    /// folder is missing them. Keeps the logged-in session and protected-world list.
    /// </summary>
    public static void MigrateFromLegacyIfNeeded()
    {
        try
        {
            var dest = Root;
            var lounge = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "introVRt Lounge");

            foreach (var folderName in LegacyProductFolderNames)
            {
                var src = Path.Combine(lounge, folderName);
                if (!Directory.Exists(src) ||
                    string.Equals(src, dest, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                foreach (var name in new[] { "auth.bin", "settings.json", "world-cache.json" })
                {
                    var from = Path.Combine(src, name);
                    var to = Path.Combine(dest, name);
                    if (!File.Exists(from) || File.Exists(to))
                    {
                        continue;
                    }

                    File.Copy(from, to, overwrite: false);
                    AppLog.Info($"Migrated {name} from legacy AppData folder '{folderName}'.");
                }
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn($"Legacy AppData migration skipped: {ex.Message}");
        }
    }
}

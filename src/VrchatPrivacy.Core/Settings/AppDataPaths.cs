using VrchatPrivacy.Core.Logging;

namespace VrchatPrivacy.Core.Settings;

/// <summary>LocalAppData root for settings, auth, cache, and logs.</summary>
public static class AppDataPaths
{
    public const string ProductFolderName = "VRChat Status Guard";
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
    /// One-shot copy of auth/settings/cache from the pre-rebrand folder when the new
    /// folder is missing them. Keeps the logged-in session and protected-world list.
    /// </summary>
    public static void MigrateFromLegacyIfNeeded()
    {
        try
        {
            var dest = Root;
            var src = LegacyRoot;
            if (!Directory.Exists(src))
            {
                return;
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
                AppLog.Info($"Migrated {name} from legacy AppData folder.");
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn($"Legacy AppData migration skipped: {ex.Message}");
        }
    }
}

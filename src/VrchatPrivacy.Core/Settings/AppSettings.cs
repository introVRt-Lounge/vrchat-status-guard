using System.Text.Json;
using System.Text.Json.Serialization;
using VrchatPrivacy.Core.Protection;
using VrchatPrivacy.Core.Worlds;

namespace VrchatPrivacy.Core.Settings;

public sealed class AppSettings
{
    /// <summary>Cuddle &amp; Sleep - operator-supplied default protected world.</summary>
    public const string DefaultProtectedWorldId = "wrld_7a0c95e2-476c-4655-9b4e-7380933c4fd3";

    public List<string> ProtectedWorldIds { get; set; } = new() { DefaultProtectedWorldId };

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ProtectionStatus ProtectionStatus { get; set; } = ProtectionStatus.Orange;

    public bool StartPaused { get; set; }

    public string? UsernameHint { get; set; }

    public static AppSettings CreateDefault() => new();

    public ProtectedWorldRules ToRules()
    {
        var worlds = new List<WorldId>();
        foreach (var id in ProtectedWorldIds)
        {
            if (WorldId.TryParse(id, out var world))
            {
                worlds.Add(world);
            }
        }

        return new ProtectedWorldRules(worlds, ProtectionStatus);
    }

    public void EnsureDefaultWorldPresent()
    {
        if (!ProtectedWorldIds.Any(id =>
                string.Equals(id, DefaultProtectedWorldId, StringComparison.OrdinalIgnoreCase)))
        {
            ProtectedWorldIds.Insert(0, DefaultProtectedWorldId);
        }
    }
}

public sealed class AppSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string _path;

    public AppSettingsStore(string? path = null)
    {
        var root = path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "introVRt Lounge",
            "VRChat Privacy Guard");
        Directory.CreateDirectory(root);
        _path = Path.Combine(root, "settings.json");
    }

    public string FilePath => _path;

    public AppSettings Load()
    {
        if (!File.Exists(_path))
        {
            var created = AppSettings.CreateDefault();
            Save(created);
            return created;
        }

        var json = File.ReadAllText(_path);
        var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? AppSettings.CreateDefault();
        settings.EnsureDefaultWorldPresent();
        return settings;
    }

    public void Save(AppSettings settings)
    {
        settings.EnsureDefaultWorldPresent();
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(_path, json);
    }
}

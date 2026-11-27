using System.Text.Json;
using System.Text.Json.Serialization;
using StatusGuard.Core.Protection;
using StatusGuard.Core.Worlds;

namespace StatusGuard.Core.Settings;

public sealed class AppSettings
{
    /// <summary>
    /// VRChat Home - the default user home world
    /// (https://vrchat.com/home/world/wrld_4432ea9b-729c-46e3-8eaf-846aa0a37fdd/info).
    /// </summary>
    public const string DefaultProtectedWorldId = "wrld_4432ea9b-729c-46e3-8eaf-846aa0a37fdd";

    public List<string> ProtectedWorldIds { get; set; } = new() { DefaultProtectedWorldId };

    /// <summary>
    /// Optional group to represent when entering a protected world (worldId → groupId).
    /// Missing key = leave representation alone for that world.
    /// </summary>
    public Dictionary<string, string> RepresentGroupByWorld { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Optional per-world status override (worldId → Orange/Red/Blue).
    /// Missing key = use <see cref="ProtectionStatus"/> default.
    /// </summary>
    public Dictionary<string, ProtectionStatus> StatusByWorld { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Default status for protected worlds without a per-world override.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ProtectionStatus ProtectionStatus { get; set; } = ProtectionStatus.Orange;

    public bool StartPaused { get; set; }

    public string? UsernameHint { get; set; }

    /// <summary>False until the first-run wizard finishes (or is skipped by migration).</summary>
    public bool OnboardingCompleted { get; set; }

    /// <summary>
    /// Early-adopter default ON (opt-out). Extreme research telemetry until toned down (#85 / #86).
    /// </summary>
    public bool TelemetryEnabled { get; set; } = true;

    /// <summary>True once the wizard (or hub Privacy) recorded an explicit telemetry choice.</summary>
    public bool TelemetryPromptShown { get; set; }

    /// <summary>Stable anonymous install id for telemetry correlation (not a VRChat id).</summary>
    public string? InstallId { get; set; }

    public ProtectionStatus StatusForWorldId(string worldId)
    {
        if (!string.IsNullOrWhiteSpace(worldId))
        {
            foreach (var kv in StatusByWorld)
            {
                if (string.Equals(kv.Key, worldId, StringComparison.OrdinalIgnoreCase))
                {
                    return kv.Value;
                }
            }
        }

        return ProtectionStatus;
    }

    /// <summary>
    /// Sets a per-world override. Pass null to fall back to <see cref="ProtectionStatus"/>.
    /// </summary>
    public void SetStatusForWorld(string worldId, ProtectionStatus? statusOrNullForDefault)
    {
        if (string.IsNullOrWhiteSpace(worldId))
        {
            return;
        }

        var toRemove = StatusByWorld.Keys
            .Where(k => string.Equals(k, worldId, StringComparison.OrdinalIgnoreCase))
            .ToList();
        foreach (var k in toRemove)
        {
            StatusByWorld.Remove(k);
        }

        if (statusOrNullForDefault is { } status)
        {
            StatusByWorld[worldId] = status;
        }
    }

    public bool HasStatusOverride(string worldId)
    {
        if (string.IsNullOrWhiteSpace(worldId))
        {
            return false;
        }

        return StatusByWorld.Keys.Any(k => string.Equals(k, worldId, StringComparison.OrdinalIgnoreCase));
    }

    public string? TryGetRepresentGroupId(string worldId)
    {
        if (string.IsNullOrWhiteSpace(worldId))
        {
            return null;
        }

        foreach (var kv in RepresentGroupByWorld)
        {
            if (string.Equals(kv.Key, worldId, StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(kv.Value))
            {
                return kv.Value;
            }
        }

        return null;
    }

    public void SetRepresentGroupId(string worldId, string? groupId)
    {
        if (string.IsNullOrWhiteSpace(worldId))
        {
            return;
        }

        var toRemove = RepresentGroupByWorld.Keys
            .Where(k => string.Equals(k, worldId, StringComparison.OrdinalIgnoreCase))
            .ToList();
        foreach (var k in toRemove)
        {
            RepresentGroupByWorld.Remove(k);
        }

        if (!string.IsNullOrWhiteSpace(groupId))
        {
            RepresentGroupByWorld[worldId] = groupId;
        }
    }

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

        return new ProtectedWorldRules(worlds, ProtectionStatus, StatusByWorld);
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
        if (path is null)
        {
            AppDataPaths.MigrateFromLegacyIfNeeded();
            var root = AppDataPaths.Root;
            Directory.CreateDirectory(root);
            _path = Path.Combine(root, "settings.json");
        }
        else
        {
            Directory.CreateDirectory(path);
            _path = Path.Combine(path, "settings.json");
        }
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
        settings.RepresentGroupByWorld ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        settings.StatusByWorld ??= new Dictionary<string, ProtectionStatus>(StringComparer.OrdinalIgnoreCase);
        // Pre-wizard installs: don't force the tour on people who already use the app.
        if (!json.Contains("onboardingCompleted", StringComparison.OrdinalIgnoreCase))
        {
            settings.OnboardingCompleted =
                !string.IsNullOrWhiteSpace(settings.UsernameHint) ||
                settings.ProtectedWorldIds.Count > 1 ||
                settings.StatusByWorld.Count > 0 ||
                settings.RepresentGroupByWorld.Count > 0;
        }

        if (string.IsNullOrWhiteSpace(settings.InstallId))
        {
            settings.InstallId = Guid.NewGuid().ToString("N");
        }

        // Early-adopter phase (#85): missing key => telemetry on (opt-out). Later tone-down may flip this (#86).
        if (!json.Contains("telemetryEnabled", StringComparison.OrdinalIgnoreCase))
        {
            settings.TelemetryEnabled = true;
        }

        settings.EnsureDefaultWorldPresent();
        return settings;
    }

    public void Save(AppSettings settings)
    {
        settings.EnsureDefaultWorldPresent();
        if (string.IsNullOrWhiteSpace(settings.InstallId))
        {
            settings.InstallId = Guid.NewGuid().ToString("N");
        }

        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(_path, json);
    }
}

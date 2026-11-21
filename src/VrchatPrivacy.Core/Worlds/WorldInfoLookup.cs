using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using VRChat.API.Client;
using VrchatPrivacy.Core.Auth;
using VrchatPrivacy.Core.Logging;

namespace VrchatPrivacy.Core.Worlds;

/// <summary>
/// Resolves world display name / thumbnail URL via VRChat GET /worlds/{id}.
/// Works without login (name + thumbnail still returned). Caches to disk.
/// </summary>
public sealed class WorldInfoLookup : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly string _cachePath;
    private readonly ConcurrentDictionary<string, WorldInfo> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly IVRChat _client;
    private readonly bool _ownsClient;

    public WorldInfoLookup(string? cacheDirectory = null, IVRChat? sharedClient = null)
    {
        var root = cacheDirectory ?? Path.Combine(
            Settings.AppDataPaths.Root);
        Directory.CreateDirectory(root);
        _cachePath = Path.Combine(root, "world-cache.json");
        LoadCache();

        if (sharedClient is not null)
        {
            _client = sharedClient;
            _ownsClient = false;
        }
        else
        {
            _client = new VRChatClientBuilder()
                .WithApplication(
                    VrchatApiSession.ApplicationName,
                    VrchatApiSession.ApplicationVersion,
                    VrchatApiSession.ApplicationContact)
                .Build();
            _ownsClient = true;
        }
    }

    public WorldInfo? TryGetCached(string worldId) =>
        _cache.TryGetValue(worldId, out var info) ? info : null;

    public void Upsert(WorldInfo info, bool persist = true)
    {
        if (string.IsNullOrWhiteSpace(info.Id))
        {
            return;
        }

        _cache[info.Id] = info;
        if (persist)
        {
            SaveCache();
        }
    }

    public void UpsertMany(IEnumerable<WorldInfo> infos)
    {
        foreach (var info in infos)
        {
            Upsert(info, persist: false);
        }

        SaveCache();
    }

    public async Task<WorldInfo> GetAsync(string worldId, CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(worldId, out var cached) &&
            !string.IsNullOrWhiteSpace(cached.Name) &&
            !string.Equals(cached.Name, worldId, StringComparison.Ordinal))
        {
            return cached;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_cache.TryGetValue(worldId, out cached) &&
                !string.IsNullOrWhiteSpace(cached.Name) &&
                !string.Equals(cached.Name, worldId, StringComparison.Ordinal))
            {
                return cached;
            }

            AppLog.Info($"WorldInfoLookup: fetching {worldId}");
            var world = await _client.Worlds.GetWorldAsync(worldId, cancellationToken).ConfigureAwait(false);
            var info = new WorldInfo(
                world.Id ?? worldId,
                string.IsNullOrWhiteSpace(world.Name) ? worldId : world.Name,
                string.IsNullOrWhiteSpace(world.ThumbnailImageUrl) ? world.ImageUrl : world.ThumbnailImageUrl,
                world.AuthorName);
            _cache[worldId] = info;
            SaveCache();
            return info;
        }
        catch (Exception ex)
        {
            AppLog.Warn($"WorldInfoLookup: failed for {worldId}: {ex.Message}");
            var fallback = new WorldInfo(worldId, worldId, null, null);
            _cache[worldId] = fallback;
            return fallback;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
    {
        _gate.Dispose();
        if (_ownsClient && _client is IDisposable d)
        {
            d.Dispose();
        }
    }

    private void LoadCache()
    {
        try
        {
            if (!File.Exists(_cachePath))
            {
                return;
            }

            var json = File.ReadAllText(_cachePath);
            var items = JsonSerializer.Deserialize<List<WorldInfo>>(json, JsonOptions);
            if (items is null)
            {
                return;
            }

            foreach (var item in items)
            {
                if (!string.IsNullOrWhiteSpace(item.Id))
                {
                    _cache[item.Id] = item;
                }
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn($"WorldInfoLookup: cache load failed: {ex.Message}");
        }
    }

    private void SaveCache()
    {
        try
        {
            var json = JsonSerializer.Serialize(_cache.Values.OrderBy(v => v.Id).ToList(), JsonOptions);
            File.WriteAllText(_cachePath, json);
        }
        catch (Exception ex)
        {
            AppLog.Warn($"WorldInfoLookup: cache save failed: {ex.Message}");
        }
    }
}

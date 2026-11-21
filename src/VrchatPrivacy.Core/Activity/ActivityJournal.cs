using System.Text.Json;
using System.Text.Json.Serialization;
using VrchatPrivacy.Core.Logging;
using VrchatPrivacy.Core.Protection;
using VrchatPrivacy.Core.Settings;
namespace VrchatPrivacy.Core.Activity;

/// <summary>
/// Persistent user-facing activity feed (Basic log). Separate from raw guard.log (Debug).
/// </summary>
public sealed class ActivityJournal
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly object _gate = new();
    private readonly List<ActivityEntry> _entries = new();
    private string? _path;
    private string? _directoryOverride;
    private bool _loaded;
    private const int MaxKept = 200;

    public event EventHandler? Changed;

    public string FilePath
    {
        get
        {
            EnsurePath();
            return _path!;
        }
    }

    public void SetDirectoryOverride(string? directoryOrNullForDefault)
    {
        lock (_gate)
        {
            _directoryOverride = directoryOrNullForDefault;
            _path = null;
            _entries.Clear();
            _loaded = false;
        }
    }

    public void RecordProtectedWorldEntered(
        string worldId,
        string? worldName,
        string? thumbnailUrl,
        ProtectionStatus status)
    {
        var name = string.IsNullOrWhiteSpace(worldName) ? ShortWorld(worldId) : worldName!;
        Record(new ActivityEntry(
            DateTimeOffset.Now,
            FeedKind.ProtectedWorldEntered,
            $"Entered {name}",
            $"Will set status to {ProtectionStatusMapping.ToUserLabel(status)}.",
            worldId,
            worldName,
            thumbnailUrl,
            status,
            null));
    }

    public void RecordStatusApplied(
        string worldId,
        string? worldName,
        string? thumbnailUrl,
        ProtectionStatus status)
    {
        var name = string.IsNullOrWhiteSpace(worldName) ? ShortWorld(worldId) : worldName!;
        Record(new ActivityEntry(
            DateTimeOffset.Now,
            FeedKind.StatusApplied,
            $"Status → {ProtectionStatusMapping.ToUserLabel(status)}",
            $"Protected world: {name}",
            worldId,
            worldName,
            thumbnailUrl,
            status,
            null));
    }

    public void RecordGroupApplied(
        string worldId,
        string? worldName,
        string? thumbnailUrl,
        ProtectionStatus status,
        string groupId)
    {
        var name = string.IsNullOrWhiteSpace(worldName) ? ShortWorld(worldId) : worldName!;
        Record(new ActivityEntry(
            DateTimeOffset.Now,
            FeedKind.GroupApplied,
            "Group representation updated",
            $"{name} · repping {groupId}",
            worldId,
            worldName,
            thumbnailUrl,
            status,
            groupId));
    }

    public void RecordPaused() =>
        Record(new ActivityEntry(
            DateTimeOffset.Now,
            FeedKind.Paused,
            "Protection paused",
            "Status Guard will not change your status until you resume.",
            null,
            null,
            null,
            null,
            null));

    public void RecordResumed() =>
        Record(new ActivityEntry(
            DateTimeOffset.Now,
            FeedKind.Resumed,
            "Protection resumed",
            "Status Guard will protect configured worlds again.",
            null,
            null,
            null,
            null,
            null));

    public IReadOnlyList<ActivityEntry> ReadRecent(int max = 80)
    {
        EnsureLoaded();
        lock (_gate)
        {
            if (_entries.Count == 0)
            {
                return Array.Empty<ActivityEntry>();
            }

            var take = Math.Min(max, _entries.Count);
            return _entries.Skip(_entries.Count - take).ToList();
        }
    }

    public void Record(ActivityEntry entry)
    {
        EnsurePath();
        lock (_gate)
        {
            EnsureLoaded_NoLock();
            _entries.Add(entry);
            while (_entries.Count > MaxKept)
            {
                _entries.RemoveAt(0);
            }

            try
            {
                File.AppendAllText(_path!, JsonSerializer.Serialize(entry, JsonOptions) + Environment.NewLine);
            }
            catch (Exception ex)
            {
                AppLog.Warn($"ActivityJournal append failed: {ex.Message}");
            }
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void EnsureLoaded()
    {
        lock (_gate)
        {
            EnsureLoaded_NoLock();
        }
    }

    private void EnsureLoaded_NoLock()
    {
        EnsurePath_NoLock();
        if (_loaded)
        {
            return;
        }

        _loaded = true;
        if (!File.Exists(_path!))
        {
            return;
        }

        try
        {
            foreach (var line in File.ReadLines(_path!))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var entry = JsonSerializer.Deserialize<ActivityEntry>(line, JsonOptions);
                if (entry is not null)
                {
                    _entries.Add(entry);
                }
            }

            while (_entries.Count > MaxKept)
            {
                _entries.RemoveAt(0);
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn($"ActivityJournal load failed: {ex.Message}");
        }
    }

    private void EnsurePath()
    {
        lock (_gate)
        {
            EnsurePath_NoLock();
        }
    }

    private void EnsurePath_NoLock()
    {
        if (_path is not null)
        {
            return;
        }

        var dir = _directoryOverride ?? AppDataPaths.Root;
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "activity.jsonl");
    }

    private static string ShortWorld(string worldId) =>
        worldId.Length <= 18 ? worldId : worldId[..14] + "…";
}

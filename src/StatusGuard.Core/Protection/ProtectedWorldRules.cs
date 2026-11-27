using StatusGuard.Core.Worlds;

namespace StatusGuard.Core.Protection;

public sealed class ProtectedWorldRules
{
    private readonly HashSet<string> _worldIds;
    private readonly Dictionary<string, ProtectionStatus> _statusByWorld;
    private ProtectionStatus _defaultStatus;

    public ProtectedWorldRules(
        IEnumerable<WorldId>? worldIds = null,
        ProtectionStatus defaultStatus = ProtectionStatus.Orange,
        IReadOnlyDictionary<string, ProtectionStatus>? statusByWorld = null)
    {
        _worldIds = new HashSet<string>(
            (worldIds ?? Enumerable.Empty<WorldId>()).Select(w => w.Value),
            StringComparer.OrdinalIgnoreCase);
        _defaultStatus = defaultStatus;
        _statusByWorld = new Dictionary<string, ProtectionStatus>(StringComparer.OrdinalIgnoreCase);
        if (statusByWorld is not null)
        {
            foreach (var kv in statusByWorld)
            {
                if (!string.IsNullOrWhiteSpace(kv.Key))
                {
                    _statusByWorld[kv.Key] = kv.Value;
                }
            }
        }
    }

    public ProtectionStatus DefaultStatus
    {
        get => _defaultStatus;
        set => _defaultStatus = value;
    }

    public IReadOnlyCollection<string> WorldIds => _worldIds;

    public bool Add(WorldId worldId) => _worldIds.Add(worldId.Value);

    public bool Remove(WorldId worldId)
    {
        _statusByWorld.Remove(worldId.Value);
        return _worldIds.Remove(worldId.Value);
    }

    public bool IsProtected(WorldId worldId) => _worldIds.Contains(worldId.Value);

    public void SetStatusOverride(WorldId worldId, ProtectionStatus? status)
    {
        if (status is null)
        {
            _statusByWorld.Remove(worldId.Value);
        }
        else
        {
            _statusByWorld[worldId.Value] = status.Value;
        }
    }

    /// <summary>
    /// Returns the status to apply when entering a protected world, or null if unprotected.
    /// Leaving a protected world never yields a restore suggestion.
    /// </summary>
    public ProtectionStatus? StatusForWorld(WorldId worldId)
    {
        if (!IsProtected(worldId))
        {
            return null;
        }

        foreach (var kv in _statusByWorld)
        {
            if (string.Equals(kv.Key, worldId.Value, StringComparison.OrdinalIgnoreCase))
            {
                return kv.Value;
            }
        }

        return _defaultStatus;
    }
}

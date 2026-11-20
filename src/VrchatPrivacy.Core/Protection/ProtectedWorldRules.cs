using VrchatPrivacy.Core.Worlds;

namespace VrchatPrivacy.Core.Protection;

public sealed class ProtectedWorldRules
{
    private readonly HashSet<string> _worldIds;
    private ProtectionStatus _defaultStatus;

    public ProtectedWorldRules(IEnumerable<WorldId>? worldIds = null, ProtectionStatus defaultStatus = ProtectionStatus.Orange)
    {
        _worldIds = new HashSet<string>(
            (worldIds ?? Enumerable.Empty<WorldId>()).Select(w => w.Value),
            StringComparer.OrdinalIgnoreCase);
        _defaultStatus = defaultStatus;
    }

    public ProtectionStatus DefaultStatus
    {
        get => _defaultStatus;
        set => _defaultStatus = value;
    }

    public IReadOnlyCollection<string> WorldIds => _worldIds;

    public bool Add(WorldId worldId) => _worldIds.Add(worldId.Value);

    public bool Remove(WorldId worldId) => _worldIds.Remove(worldId.Value);

    public bool IsProtected(WorldId worldId) => _worldIds.Contains(worldId.Value);

    /// <summary>
    /// Returns the status to apply when entering a protected world, or null if unprotected.
    /// Leaving a protected world never yields a restore suggestion.
    /// </summary>
    public ProtectionStatus? StatusForWorld(WorldId worldId) =>
        IsProtected(worldId) ? _defaultStatus : null;
}

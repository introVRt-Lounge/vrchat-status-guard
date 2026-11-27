using System.Text.RegularExpressions;

namespace StatusGuard.Core.Worlds;

/// <summary>
/// VRChat world identifier (wrld_...). Instance ids and names are intentionally ignored.
/// </summary>
public readonly partial struct WorldId : IEquatable<WorldId>
{
    private static readonly Regex WorldIdRegex = WorldIdPattern();

    public string Value { get; }

    private WorldId(string value) => Value = value;

    public static bool TryParse(string? input, out WorldId worldId)
    {
        worldId = default;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var trimmed = input.Trim();
        if (!WorldIdRegex.IsMatch(trimmed))
        {
            return false;
        }

        worldId = new WorldId(trimmed);
        return true;
    }

    /// <summary>
    /// Accepts a bare world id or a VRChat world URL containing wrld_...
    /// </summary>
    public static bool TryParseFromUserInput(string? input, out WorldId worldId)
    {
        worldId = default;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var trimmed = input.Trim();
        if (TryParse(trimmed, out worldId))
        {
            return true;
        }

        var match = EmbeddedWorldIdPattern().Match(trimmed);
        if (!match.Success)
        {
            return false;
        }

        return TryParse(match.Value, out worldId);
    }

    public bool Equals(WorldId other) =>
        string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);

    public override bool Equals(object? obj) => obj is WorldId other && Equals(other);

    public override int GetHashCode() =>
        Value is null ? 0 : StringComparer.OrdinalIgnoreCase.GetHashCode(Value);

    public override string ToString() => Value ?? string.Empty;

    public static bool operator ==(WorldId left, WorldId right) => left.Equals(right);

    public static bool operator !=(WorldId left, WorldId right) => !left.Equals(right);

    [GeneratedRegex(@"^wrld_[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$", RegexOptions.CultureInvariant)]
    private static partial Regex WorldIdPattern();

    [GeneratedRegex(@"wrld_[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}", RegexOptions.CultureInvariant)]
    private static partial Regex EmbeddedWorldIdPattern();
}

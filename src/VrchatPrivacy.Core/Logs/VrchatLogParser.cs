using System.Globalization;
using System.Text.RegularExpressions;
using VrchatPrivacy.Core.Worlds;

namespace VrchatPrivacy.Core.Logs;

/// <summary>
/// Parses VRChat PC client output_log lines for world joins.
/// Verified against live logs (2026-11-20): "[Behaviour] Joining wrld_...:instance..."
/// </summary>
public static partial class VrchatLogParser
{
    /// <summary>
    /// Attempts to parse a world join from a single log line.
    /// Matches world id only; discards instance privacy tokens beyond the raw instance segment.
    /// </summary>
    public static bool TryParseWorldJoin(string? line, out WorldJoinEvent? joinEvent)
    {
        joinEvent = null;
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        var match = JoiningWorldPattern().Match(line);
        if (!match.Success)
        {
            return false;
        }

        if (!WorldId.TryParse(match.Groups["worldId"].Value, out var worldId))
        {
            return false;
        }

        DateTimeOffset? observedAt = null;
        if (DateTimeOffset.TryParseExact(
                match.Groups["timestamp"].Value,
                "yyyy.MM.dd HH:mm:ss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeLocal,
                out var parsed))
        {
            observedAt = parsed;
        }

        var instance = match.Groups["instance"].Success
            ? match.Groups["instance"].Value
            : null;

        joinEvent = new WorldJoinEvent(worldId, instance, observedAt);
        return true;
    }

    /// <summary>
    /// Scans chronologically ordered lines and returns the last successful world join.
    /// </summary>
    public static WorldJoinEvent? FindLatestWorldJoin(IEnumerable<string> lines)
    {
        WorldJoinEvent? latest = null;
        foreach (var line in lines)
        {
            if (TryParseWorldJoin(line, out var join) && join is not null)
            {
                latest = join;
            }
        }

        return latest;
    }

    // Example:
    // 2026.11.20 00:42:39 Debug      -  [Behaviour] Joining wrld_4432ea9b-729c-46e3-8eaf-846aa0a37fdd:28829~friends(...)~region(eu)
    [GeneratedRegex(
        @"^(?<timestamp>\d{4}\.\d{2}\.\d{2}\s+\d{2}:\d{2}:\d{2}).*?\[Behaviour\]\s+Joining\s+(?<worldId>wrld_[0-9a-fA-F-]{36}):(?<instance>\S+)",
        RegexOptions.CultureInvariant | RegexOptions.Compiled)]
    private static partial Regex JoiningWorldPattern();
}

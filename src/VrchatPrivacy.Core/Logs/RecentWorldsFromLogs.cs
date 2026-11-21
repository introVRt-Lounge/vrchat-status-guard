using System.Text;
using VrchatPrivacy.Core.Worlds;

namespace VrchatPrivacy.Core.Logs;

/// <summary>Pulls distinct recently joined world IDs from VRChat output_log files.</summary>
public static class RecentWorldsFromLogs
{
    public static IReadOnlyList<string> GetRecentWorldIds(int max = 20, string? logDirectory = null)
    {
        var dir = logDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "AppData",
            "LocalLow",
            "VRChat",
            "VRChat");

        if (!Directory.Exists(dir))
        {
            return Array.Empty<string>();
        }

        var files = Directory.EnumerateFiles(dir, "output_log_*.txt")
            .Select(p => new FileInfo(p))
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .Take(3)
            .ToList();

        var ordered = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            try
            {
                using var stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
                var joins = new List<string>();
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    if (VrchatLogParser.TryParseWorldJoin(line, out var join) && join is not null)
                    {
                        joins.Add(join.WorldId.Value);
                    }
                }

                // Newest joins last in file → walk reverse for recency.
                for (var i = joins.Count - 1; i >= 0; i--)
                {
                    if (seen.Add(joins[i]))
                    {
                        ordered.Add(joins[i]);
                        if (ordered.Count >= max)
                        {
                            return ordered;
                        }
                    }
                }
            }
            catch (IOException)
            {
                // skip locked/partial files
            }
        }

        return ordered;
    }
}

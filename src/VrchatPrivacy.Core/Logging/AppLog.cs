namespace VrchatPrivacy.Core.Logging;

/// <summary>Simple append-only log under LocalAppData for operator diagnosis.</summary>
public static class AppLog
{
    private static readonly object Gate = new();
    private static string? _path;
    private static string? _directoryOverride;

    public static string FilePath
    {
        get
        {
            EnsurePath();
            return _path!;
        }
    }

    /// <summary>
    /// Redirect log output (e.g. unit tests) so operator guard.log is not polluted.
    /// Pass null to restore the default AppData path. Resets the cached file path.
    /// </summary>
    public static void SetDirectoryOverride(string? directoryOrNullForDefault)
    {
        lock (Gate)
        {
            _directoryOverride = directoryOrNullForDefault;
            _path = null;
        }
    }

    public static void Info(string message) => Write("INFO", message);

    public static void Warn(string message) => Write("WARN", message);

    public static void Error(string message, Exception? ex = null)
    {
        Write("ERROR", ex is null ? message : $"{message} | {ex.GetType().Name}: {ex.Message}");
        if (ex is not null)
        {
            Write("ERROR", ex.ToString());
        }
    }

    /// <summary>
    /// Reads the last <paramref name="maxLines"/> lines from the log without blocking writers.
    /// </summary>
    public static string ReadRecent(int maxLines = 400)
    {
        if (maxLines < 1)
        {
            maxLines = 1;
        }

        EnsurePath();
        var path = _path!;
        if (!File.Exists(path))
        {
            return "(Log file not created yet - activity will appear here once Status Guard runs.)";
        }

        try
        {
            using var fs = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(fs);
            var lines = new List<string>();
            while (reader.ReadLine() is { } line)
            {
                lines.Add(line);
                if (lines.Count > maxLines)
                {
                    lines.RemoveAt(0);
                }
            }

            return lines.Count == 0
                ? "(Log is empty.)"
                : string.Join(Environment.NewLine, lines);
        }
        catch (Exception ex)
        {
            return $"(Could not read log: {ex.Message})";
        }
    }

    private static void EnsurePath()
    {
        if (_path is not null)
        {
            return;
        }

        lock (Gate)
        {
            if (_path is not null)
            {
                return;
            }

            var dir = _directoryOverride ?? Settings.AppDataPaths.Root;
            Directory.CreateDirectory(dir);
            _path = Path.Combine(dir, "guard.log");
        }
    }

    private static void Write(string level, string message)
    {
        EnsurePath();
        var line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}{Environment.NewLine}";
        lock (Gate)
        {
            File.AppendAllText(_path!, line);
        }
    }
}

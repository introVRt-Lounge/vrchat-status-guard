namespace VrchatPrivacy.Core.Logging;

/// <summary>Simple append-only log under LocalAppData for operator diagnosis.</summary>
public static class AppLog
{
    private static readonly object Gate = new();
    private static string? _path;

    public static string FilePath
    {
        get
        {
            EnsurePath();
            return _path!;
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

    private static void EnsurePath()
    {
        if (_path is not null)
        {
            return;
        }

        var dir = Settings.AppDataPaths.Root;
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "guard.log");
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

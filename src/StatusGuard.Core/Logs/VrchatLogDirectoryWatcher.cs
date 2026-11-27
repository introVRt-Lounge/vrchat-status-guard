using System.Text;

namespace StatusGuard.Core.Logs;

/// <summary>
/// Tails the newest VRChat output_log_*.txt under LocalLow\VRChat\VRChat.
/// </summary>
public sealed class VrchatLogDirectoryWatcher : IVrchatLogWatcher, IDisposable
{
    private readonly string _logDirectory;
    private readonly object _gate = new();
    private FileSystemWatcher? _watcher;
    private CancellationTokenSource? _cts;
    private string? _activePath;
    private long _position;
    private Timer? _pollTimer;

    public VrchatLogDirectoryWatcher(string? logDirectory = null)
    {
        _logDirectory = logDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "AppData",
            "LocalLow",
            "VRChat",
            "VRChat");
    }

    public event EventHandler<WorldJoinEvent>? WorldJoined;

    public string LogDirectory => _logDirectory;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_logDirectory);
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        _watcher = new FileSystemWatcher(_logDirectory)
        {
            Filter = "output_log_*.txt",
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
            EnableRaisingEvents = true,
        };
        _watcher.Created += (_, _) => SwitchToNewest();
        _watcher.Changed += (_, e) =>
        {
            if (string.Equals(e.FullPath, _activePath, StringComparison.OrdinalIgnoreCase))
            {
                ReadNewLines();
            }
            else
            {
                SwitchToNewest();
            }
        };

        // Seek to EOF only. Orchestrator does a single GetCurrentWorldAsync catch-up;
        // do not replay historical joins or double-emit current world.
        SwitchToNewest(seekToEnd: true);
        _pollTimer = new Timer(_ => ReadNewLines(), null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        _pollTimer?.Dispose();
        _pollTimer = null;
        _watcher?.Dispose();
        _watcher = null;
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        return Task.CompletedTask;
    }

    public Task<WorldJoinEvent?> GetCurrentWorldAsync(CancellationToken cancellationToken = default)
    {
        var newest = FindNewestLog();
        if (newest is null)
        {
            return Task.FromResult<WorldJoinEvent?>(null);
        }

        // Read with shared access; Unity keeps the file open.
        using var stream = new FileStream(newest, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var lines = new List<string>();
        while (!reader.EndOfStream)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = reader.ReadLine();
            if (line is not null)
            {
                lines.Add(line);
            }
        }

        return Task.FromResult(VrchatLogParser.FindLatestWorldJoin(lines));
    }

    public void Dispose()
    {
        StopAsync().GetAwaiter().GetResult();
    }

    private void SwitchToNewest(bool seekToEnd = false)
    {
        lock (_gate)
        {
            var newest = FindNewestLog();
            if (newest is null)
            {
                return;
            }

            if (!string.Equals(newest, _activePath, StringComparison.OrdinalIgnoreCase) || seekToEnd)
            {
                _activePath = newest;
                // Always open at EOF. Never set position=0 and Replay the whole
                // output_log - that flooded Activity + re-applied status on every
                // Status Guard relaunch while the operator sat idle in Home (#56).
                _position = new FileInfo(newest).Length;
            }
        }

        if (seekToEnd)
        {
            // First open: nothing to read yet; poll / Changed tails new lines.
            return;
        }

        ReadNewLines();
    }

    private void ReadNewLines()
    {
        string? path;
        long start;
        lock (_gate)
        {
            path = _activePath;
            start = _position;
        }

        if (path is null || !File.Exists(path))
        {
            return;
        }

        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (stream.Length < start)
            {
                start = 0;
            }

            stream.Seek(start, SeekOrigin.Begin);
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            string? line;
            while ((line = reader.ReadLine()) is not null)
            {
                if (VrchatLogParser.TryParseWorldJoin(line, out var join) && join is not null)
                {
                    WorldJoined?.Invoke(this, join);
                }
            }

            lock (_gate)
            {
                if (string.Equals(path, _activePath, StringComparison.OrdinalIgnoreCase))
                {
                    _position = stream.Position;
                }
            }
        }
        catch (IOException)
        {
            // File briefly locked; poll timer retries.
        }
    }

    private string? FindNewestLog()
    {
        if (!Directory.Exists(_logDirectory))
        {
            return null;
        }

        return Directory.EnumerateFiles(_logDirectory, "output_log_*.txt")
            .Select(p => new FileInfo(p))
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .Select(f => f.FullName)
            .FirstOrDefault();
    }
}

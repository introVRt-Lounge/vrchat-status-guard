using System.Diagnostics;
using StatusGuard.Core.Logging;

namespace StatusGuard.Core.Logs;

/// <summary>
/// Polls for the Windows VRChat client process. Friendly name is <c>VRChat</c>
/// (no .exe) - matches VRCX / .NET GetProcessesByName convention.
/// </summary>
public sealed class VrchatProcessPresence : IVrchatClientPresence
{
    public const string ProcessName = "VRChat";

    private readonly TimeSpan _interval;
    private readonly object _gate = new();
    private Timer? _timer;
    private bool _running;
    private bool _known;

    public VrchatProcessPresence(TimeSpan? pollInterval = null)
    {
        _interval = pollInterval ?? TimeSpan.FromSeconds(2);
    }

    public bool IsRunning
    {
        get
        {
            lock (_gate)
            {
                return _running;
            }
        }
    }

    public event EventHandler<bool>? Changed;

    public void Start()
    {
        lock (_gate)
        {
            if (_timer is not null)
            {
                return;
            }

            Probe_NoLock(raise: true);
            _timer = new Timer(_ => Probe(), null, _interval, _interval);
        }
    }

    public void Stop()
    {
        lock (_gate)
        {
            _timer?.Dispose();
            _timer = null;
        }
    }

    public void Dispose() => Stop();

    public static bool ProbeOnce()
    {
        Process[] procs;
        try
        {
            procs = Process.GetProcessesByName(ProcessName);
        }
        catch
        {
            return false;
        }

        try
        {
            return procs.Length > 0;
        }
        finally
        {
            foreach (var p in procs)
            {
                p.Dispose();
            }
        }
    }

    private void Probe()
    {
        lock (_gate)
        {
            Probe_NoLock(raise: true);
        }
    }

    private void Probe_NoLock(bool raise)
    {
        var now = ProbeOnce();
        if (_known && now == _running)
        {
            return;
        }

        var wasKnown = _known;
        var previous = _running;
        _running = now;
        _known = true;
        if (!wasKnown || previous != now)
        {
            AppLog.Info(now ? "VRChat client: running" : "VRChat client: not running");
            if (raise)
            {
                // Raise outside lock via captured value.
                var running = now;
                ThreadPool.QueueUserWorkItem(_ => Changed?.Invoke(this, running));
            }
        }
    }
}

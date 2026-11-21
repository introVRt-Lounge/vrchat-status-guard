namespace VrchatPrivacy.Core.Logs;

/// <summary>Whether the VRChat game client process is running on this PC.</summary>
public interface IVrchatClientPresence : IDisposable
{
    bool IsRunning { get; }

    /// <summary>Raised when <see cref="IsRunning"/> flips (true = client started).</summary>
    event EventHandler<bool>? Changed;

    void Start();

    void Stop();
}

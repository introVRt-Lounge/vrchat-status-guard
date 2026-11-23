namespace VrchatPrivacy.Core.Telemetry;

/// <summary>Process-wide facade so Core and App can emit without DI ceremony.</summary>
public static class TelemetryGate
{
    private static TelemetryClient? _client;

    public static bool IsEnabled => _client?.IsEnabled == true;

    public static void Configure(TelemetryClient? client)
    {
        var previous = Interlocked.Exchange(ref _client, client);
        previous?.Dispose();
    }

    public static void SetEnabled(bool enabled) => _client?.SetEnabled(enabled);

    public static void Track(string name, IReadOnlyDictionary<string, string?>? props = null) =>
        _client?.Track(name, props);

    public static void Track(string name, params (string Key, string? Value)[] props)
    {
        if (_client is null || props.Length == 0)
        {
            _client?.Track(name, null);
            return;
        }

        var map = new Dictionary<string, string?>(props.Length, StringComparer.Ordinal);
        foreach (var (key, value) in props)
        {
            map[key] = value;
        }

        _client.Track(name, map);
    }

    public static Task FlushAsync(CancellationToken ct = default) =>
        _client?.FlushAsync(ct) ?? Task.CompletedTask;
}

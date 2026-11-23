using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using VrchatPrivacy.Core.Logging;

namespace VrchatPrivacy.Core.Telemetry;

/// <summary>
/// Extreme early-adopter telemetry: batched JSON POSTs to estate Fluent Bit via Cloudflare.
/// Allowlisted props only; scrubber strips path/user-looking strings. No-ops when disabled.
/// </summary>
public sealed class TelemetryClient : IDisposable
{
    public const string DefaultEndpoint = "https://logs.introvrtlounge.com/";
    public const int SchemaVersion = 1;

    private static readonly Regex Secretish = new(
        @"(?i)(authcookie|otp|password|token|bearer\s+[a-z0-9\-._~+/]+=*)",
        RegexOptions.Compiled);
    private static readonly Regex UserPath = new(
        @"(?i)([a-z]:\\users\\)[^\\\/]+",
        RegexOptions.Compiled);

    private readonly HttpClient _http;
    private readonly string _endpoint;
    private readonly string _installId;
    private readonly string _appVersion;
    private readonly string _osVersion;
    private readonly ConcurrentQueue<TelemetryEvent> _queue = new();
    private readonly object _flushGate = new();
    private readonly DateTimeOffset _sessionStartedUtc = DateTimeOffset.UtcNow;
    private int _seq;
    private bool _enabled;
    private bool _disposed;
    private int _flushing;

    public TelemetryClient(
        string installId,
        string appVersion,
        bool enabled,
        string? endpoint = null,
        HttpClient? http = null)
    {
        _installId = string.IsNullOrWhiteSpace(installId) ? Guid.NewGuid().ToString("N") : installId.Trim();
        _appVersion = string.IsNullOrWhiteSpace(appVersion) ? "0.0.0" : appVersion.Trim();
        _osVersion = Environment.OSVersion.VersionString;
        _endpoint = string.IsNullOrWhiteSpace(endpoint) ? DefaultEndpoint : endpoint.Trim();
        _enabled = enabled;
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
        if (http is null)
        {
            _http.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue("StatusGuard", _appVersion));
            _http.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
        }
    }

    public bool IsEnabled => _enabled && !_disposed;

    public string InstallId => _installId;

    public void SetEnabled(bool enabled)
    {
        _enabled = enabled;
        if (!enabled)
        {
            while (_queue.TryDequeue(out _))
            {
                // Drop pending when user opts out.
            }
        }
    }

    public void Track(string name, IReadOnlyDictionary<string, string?>? props = null)
    {
        if (!_enabled || _disposed || string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        var scrubbed = ScrubProps(props);
        var dense = DateTimeOffset.UtcNow - _sessionStartedUtc < TimeSpan.FromMinutes(30);
        scrubbed["session_dense"] = dense ? "1" : "0";
        scrubbed["session_seq"] = Interlocked.Increment(ref _seq).ToString();

        _queue.Enqueue(new TelemetryEvent(
            Name: name.Trim(),
            Ts: DateTimeOffset.UtcNow.ToString("o"),
            Props: scrubbed));

        if (_queue.Count >= 12)
        {
            _ = FlushAsync();
        }
    }

    public async Task FlushAsync(CancellationToken ct = default)
    {
        if (!_enabled || _disposed)
        {
            return;
        }

        if (Interlocked.CompareExchange(ref _flushing, 1, 0) != 0)
        {
            return;
        }

        try
        {
            List<TelemetryEvent> batch;
            lock (_flushGate)
            {
                batch = new List<TelemetryEvent>(16);
                while (batch.Count < 40 && _queue.TryDequeue(out var ev))
                {
                    batch.Add(ev);
                }
            }

            if (batch.Count == 0)
            {
                return;
            }

            var payload = new
            {
                source = "status-guard",
                schema = SchemaVersion,
                install_id = _installId,
                app_version = _appVersion,
                os = _osVersion,
                events = batch.Select(e => new
                {
                    name = e.Name,
                    ts = e.Ts,
                    props = e.Props,
                }),
            };

            var json = JsonSerializer.Serialize(payload);
            // Bare application/json (no charset) - some HTTP intakes are picky.
            using var content = new StringContent(
                json,
                Encoding.UTF8,
                new MediaTypeHeaderValue("application/json"));
            using var response = await _http.PostAsync(_endpoint, content, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                AppLog.Warn($"Telemetry flush HTTP {(int)response.StatusCode}");
                // Re-queue once on failure (best effort).
                foreach (var ev in batch)
                {
                    _queue.Enqueue(ev);
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            AppLog.Warn($"Telemetry flush failed: {ex.GetType().Name}");
        }
        finally
        {
            Interlocked.Exchange(ref _flushing, 0);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            // Flush while still enabled; FlushAsync no-ops after _disposed.
            FlushAsync().GetAwaiter().GetResult();
        }
        catch (Exception)
        {
            // ignore on dispose
        }

        _disposed = true;
        _http.Dispose();
    }

    public static Dictionary<string, string?> ScrubProps(IReadOnlyDictionary<string, string?>? props)
    {
        var map = new Dictionary<string, string?>(StringComparer.Ordinal);
        if (props is null)
        {
            return map;
        }

        foreach (var kv in props)
        {
            if (string.IsNullOrWhiteSpace(kv.Key))
            {
                continue;
            }

            // Never allow raw identity-ish keys through even if a caller slips.
            var key = kv.Key.Trim();
            if (key.Contains("user", StringComparison.OrdinalIgnoreCase) ||
                key.Contains("cookie", StringComparison.OrdinalIgnoreCase) ||
                key.Contains("password", StringComparison.OrdinalIgnoreCase) ||
                key.Contains("otp", StringComparison.OrdinalIgnoreCase) ||
                key.Contains("world_id", StringComparison.OrdinalIgnoreCase) ||
                key.Contains("group_id", StringComparison.OrdinalIgnoreCase) ||
                key.Contains("instance", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            map[key] = ScrubValue(kv.Value);
        }

        return map;
    }

    public static string? ScrubValue(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        var v = Secretish.Replace(value, "[redacted]");
        v = UserPath.Replace(v, "$1[user]");
        if (v.Length > 240)
        {
            v = v[..240] + "…";
        }

        return v;
    }

    private readonly record struct TelemetryEvent(
        string Name,
        string Ts,
        Dictionary<string, string?> Props);
}

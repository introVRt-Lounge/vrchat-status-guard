using System.Diagnostics;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using Velopack;
using Velopack.Sources;

namespace VrchatPrivacy.App.Updates;

/// <summary>
/// Velopack update checks (when installed via Velopack) plus GitHub release-notes fetch for What's New.
/// </summary>
public sealed class AppUpdateService
{
    public const string RepoUrl = "https://github.com/introVRt-Lounge/vrchat-status-guard";
    private const string ReleasesApi = "https://api.github.com/repos/introVRt-Lounge/vrchat-status-guard/releases";

    private readonly UpdateManager _manager;

    public AppUpdateService()
    {
        var source = new GithubSource(RepoUrl, accessToken: null, prerelease: false);
        _manager = new UpdateManager(source);
    }

    public bool IsVelopackInstalled => _manager.IsInstalled;

    public static string CurrentVersion
    {
        get
        {
            var asm = Assembly.GetExecutingAssembly();
            var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (!string.IsNullOrWhiteSpace(info))
            {
                var plus = info.IndexOf('+');
                return plus > 0 ? info[..plus] : info;
            }

            return asm.GetName().Version?.ToString(3) ?? "0.0.0";
        }
    }

    public async Task<UpdateInfo?> CheckForUpdatesAsync(CancellationToken ct = default)
    {
        if (!_manager.IsInstalled)
        {
            return null;
        }

        ct.ThrowIfCancellationRequested();
        return await _manager.CheckForUpdatesAsync().ConfigureAwait(false);
    }

    public async Task DownloadAndApplyAsync(UpdateInfo update, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        await _manager.DownloadUpdatesAsync(update, progress: null).ConfigureAwait(false);
        _manager.ApplyUpdatesAndRestart(update);
    }

    public async Task<ReleaseNotes> GetReleaseNotesAsync(string? version = null, CancellationToken ct = default)
    {
        version ??= CurrentVersion;
        var tag = version.StartsWith('v') ? version : "v" + version;

        using var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("VRChat-Status-Guard", CurrentVersion));
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        // Exact tag first
        var exact = await http.GetAsync($"{ReleasesApi}/tags/{tag}", ct).ConfigureAwait(false);
        if (exact.IsSuccessStatusCode)
        {
            return await ParseReleaseAsync(exact, tag).ConfigureAwait(false);
        }

        // Latest as fallback
        var latest = await http.GetAsync($"{ReleasesApi}/latest", ct).ConfigureAwait(false);
        if (latest.IsSuccessStatusCode)
        {
            var notes = await ParseReleaseAsync(latest, tag).ConfigureAwait(false);
            notes.MatchedExactTag = false;
            return notes;
        }

        return new ReleaseNotes
        {
            Tag = tag,
            Title = "Release notes unavailable",
            Body = exact.StatusCode == System.Net.HttpStatusCode.NotFound
                ? $"No GitHub release found for {tag}, and latest is also unavailable ({(int)latest.StatusCode})."
                : $"Could not load release notes ({(int)exact.StatusCode}). Check your network.",
            MatchedExactTag = false,
        };
    }

    private static async Task<ReleaseNotes> ParseReleaseAsync(HttpResponseMessage response, string requestedTag)
    {
        await using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(stream).ConfigureAwait(false);
        var root = doc.RootElement;
        var tag = root.TryGetProperty("tag_name", out var t) ? t.GetString() ?? requestedTag : requestedTag;
        var name = root.TryGetProperty("name", out var n) ? n.GetString() : null;
        var body = root.TryGetProperty("body", out var b) ? b.GetString() : null;
        var url = root.TryGetProperty("html_url", out var u) ? u.GetString() : RepoUrl + "/releases";

        return new ReleaseNotes
        {
            Tag = tag,
            Title = string.IsNullOrWhiteSpace(name) ? tag : name!,
            Body = string.IsNullOrWhiteSpace(body) ? "(No release notes were published for this version.)" : body!,
            HtmlUrl = url,
            MatchedExactTag = string.Equals(tag, requestedTag, StringComparison.OrdinalIgnoreCase),
        };
    }

    public static void OpenReleasesPage()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = RepoUrl + "/releases",
            UseShellExecute = true,
        });
    }
}

public sealed class ReleaseNotes
{
    public required string Tag { get; init; }
    public required string Title { get; init; }
    public required string Body { get; init; }
    public string? HtmlUrl { get; init; }
    public bool MatchedExactTag { get; set; } = true;
}

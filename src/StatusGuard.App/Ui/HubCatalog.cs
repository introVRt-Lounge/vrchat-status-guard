using System.Collections.Concurrent;
using StatusGuard.Core.Auth;
using StatusGuard.Core.Groups;
using StatusGuard.Core.Logs;
using StatusGuard.Core.Logging;
using StatusGuard.Core.Worlds;

namespace StatusGuard.App.Ui;

/// <summary>
/// Shared in-memory catalog warmed at tray startup. UI reads from here —
/// never hammer the API on every paint/selection.
/// </summary>
public sealed class HubCatalog : IDisposable
{
    private readonly IVrchatSession _session;
    private readonly WorldInfoLookup _worlds;
    private readonly ConcurrentDictionary<string, Image> _images = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(20) };
    private IReadOnlyList<WorldInfo> _recent = Array.Empty<WorldInfo>();
    private IReadOnlyList<GroupInfo> _groups = Array.Empty<GroupInfo>();
    private DateTimeOffset _recentFetchedUtc = DateTimeOffset.MinValue;
    private DateTimeOffset _groupsFetchedUtc = DateTimeOffset.MinValue;
    private bool _warming;

    public HubCatalog(IVrchatSession session, WorldInfoLookup? worlds = null)
    {
        _session = session;
        _worlds = worlds ?? new WorldInfoLookup();
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "StatusGuard/0.1.0 (status-guard@introvrtlounge.com)");
    }

    public WorldInfoLookup Worlds => _worlds;
    public IReadOnlyList<WorldInfo> RecentWorlds => _recent;
    public IReadOnlyList<GroupInfo> Groups => _groups;
    public bool IsWarming => _warming;
    public bool HasRecent => _recent.Count > 0;
    public bool HasGroups => _groups.Count > 0;

    public event EventHandler? Changed;

    public void BeginWarmup() => _ = WarmupAsync(force: false);

    public async Task WarmupAsync(bool force, CancellationToken ct = default)
    {
        if (!force && _warming)
        {
            return;
        }

        List<WorldInfo> recentSnapshot;
        List<GroupInfo> groupSnapshot;
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            _warming = true;
            Changed?.Invoke(this, EventArgs.Empty);

            await RefreshRecentAsync(force, ct).ConfigureAwait(false);
            await RefreshGroupsAsync(force, ct).ConfigureAwait(false);
            recentSnapshot = _recent.Take(20).ToList();
            groupSnapshot = _groups.Take(40).ToList();
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            AppLog.Warn($"HubCatalog warmup failed: {ex.Message}");
            return;
        }
        finally
        {
            _warming = false;
            _gate.Release();
            Changed?.Invoke(this, EventArgs.Empty);
        }

        // Image prefetch outside the catalog lock so the hub can open immediately.
        foreach (var w in recentSnapshot)
        {
            ct.ThrowIfCancellationRequested();
            _ = GetImageAsync(w.ThumbnailImageUrl, 160, 90, ct);
        }

        foreach (var g in groupSnapshot)
        {
            ct.ThrowIfCancellationRequested();
            _ = GetImageAsync(g.IconUrl ?? g.BannerUrl, 72, 72, ct);
        }
    }

    public async Task EnsureFreshForHubAsync(CancellationToken ct = default)
    {
        // Soft refresh if older than 5 minutes; otherwise use memory.
        var stale = DateTimeOffset.UtcNow - _recentFetchedUtc > TimeSpan.FromMinutes(5) ||
                    DateTimeOffset.UtcNow - _groupsFetchedUtc > TimeSpan.FromMinutes(5);
        if (stale || _recent.Count == 0 || _groups.Count == 0)
        {
            await WarmupAsync(force: true, ct).ConfigureAwait(false);
        }
    }

    public GroupInfo? FindGroup(string? groupId)
    {
        if (string.IsNullOrWhiteSpace(groupId))
        {
            return null;
        }

        return _groups.FirstOrDefault(g =>
            string.Equals(g.Id, groupId, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<Image> GetImageAsync(string? url, int w, int h, CancellationToken ct = default)
    {
        var resolved = VrchatImageUrl.PreferThumbnail(url, size: Math.Max(w, h));
        if (string.IsNullOrWhiteSpace(resolved))
        {
            return Placeholder(w, h, "?");
        }

        var key = $"{w}x{h}|{resolved}";
        if (_images.TryGetValue(key, out var cached))
        {
            return cached;
        }

        try
        {
            await using var stream = await _http.GetStreamAsync(resolved, ct).ConfigureAwait(false);
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms, ct).ConfigureAwait(false);
            ms.Position = 0;
            using var img = Image.FromStream(ms);
            var bmp = new Bitmap(img, new Size(w, h));
            _images[key] = bmp;
            return bmp;
        }
        catch (Exception ex)
        {
            AppLog.Warn($"HubCatalog image failed: {ex.Message}");
            // Do not cache failures - next open / retry can recover.
            return Placeholder(w, h, "?");
        }
    }

    public Image? TryGetCachedImage(string? url, int w, int h)
    {
        var resolved = VrchatImageUrl.PreferThumbnail(url, size: Math.Max(w, h));
        if (string.IsNullOrWhiteSpace(resolved))
        {
            return null;
        }

        return _images.TryGetValue($"{w}x{h}|{resolved}", out var img) ? img : null;
    }

    public void Dispose()
    {
        _gate.Dispose();
        _http.Dispose();
        _worlds.Dispose();
        foreach (var img in _images.Values)
        {
            img.Dispose();
        }

        _images.Clear();
    }

    private async Task RefreshRecentAsync(bool force, CancellationToken ct)
    {
        if (!force && _recent.Count > 0 &&
            DateTimeOffset.UtcNow - _recentFetchedUtc < TimeSpan.FromMinutes(5))
        {
            return;
        }

        IReadOnlyList<WorldInfo> recent = Array.Empty<WorldInfo>();
        if (_session.IsAuthenticated)
        {
            recent = await _session.GetRecentWorldsAsync(20, ct).ConfigureAwait(false);
        }

        if (recent.Count == 0)
        {
            var ids = RecentWorldsFromLogs.GetRecentWorldIds(20);
            var built = new List<WorldInfo>();
            foreach (var id in ids)
            {
                ct.ThrowIfCancellationRequested();
                built.Add(await _worlds.GetAsync(id, ct).ConfigureAwait(false));
            }

            recent = built;
        }
        _worlds.UpsertMany(recent);
        _recent = recent;
        _recentFetchedUtc = DateTimeOffset.UtcNow;
        AppLog.Info($"HubCatalog: recent={_recent.Count}");
    }

    private async Task RefreshGroupsAsync(bool force, CancellationToken ct)
    {
        if (!force && _groups.Count > 0 &&
            DateTimeOffset.UtcNow - _groupsFetchedUtc < TimeSpan.FromMinutes(5))
        {
            return;
        }

        if (_session.IsAuthenticated)
        {
            _groups = await _session.GetUserGroupsAsync(ct).ConfigureAwait(false);
        }
        else
        {
            _groups = LocalVrchatGroups.ReadGroupOrder()
                .Select(id => new GroupInfo(id, id, null, null, null, null, false))
                .ToList();
        }

        _groupsFetchedUtc = DateTimeOffset.UtcNow;
        AppLog.Info($"HubCatalog: groups={_groups.Count}");
    }

    private static Image Placeholder(int w, int h, string glyph)
    {
        var bmp = new Bitmap(w, h);
        using var g = Graphics.FromImage(bmp);
        g.Clear(VrcTheme.BgElevated);
        using var pen = new Pen(VrcTheme.Border);
        g.DrawRectangle(pen, 0, 0, w - 1, h - 1);
        TextRenderer.DrawText(
            g,
            glyph,
            VrcTheme.HeadingFont,
            new Rectangle(0, 0, w, h),
            VrcTheme.TextMuted,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        return bmp;
    }
}

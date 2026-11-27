using System.Text.RegularExpressions;

namespace StatusGuard.Core.Groups;

/// <summary>
/// VRChat serves full files at /api/1/file/.../file and resized thumbs at /api/1/image/.../{size}.
/// Prefer the image endpoint for UI logos (e.g. group icons).
/// </summary>
public static partial class VrchatImageUrl
{
    [GeneratedRegex(
        @"^https://api\.vrchat\.cloud/api/1/(?:file|image)/(file_[0-9a-fA-F\-]+)/(\d+)(?:/file|/\d+)?/?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FileOrImage();

    /// <summary>
    /// Rewrites api.vrchat.cloud /file/.../file (or /image/.../N) to /image/.../{size}.
    /// Non-matching URLs are returned unchanged.
    /// </summary>
    public static string? PreferThumbnail(string? url, int size = 256)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        if (size < 16)
        {
            size = 16;
        }

        var trimmed = url.Trim();
        var m = FileOrImage().Match(trimmed);
        if (!m.Success)
        {
            return trimmed;
        }

        var fileId = m.Groups[1].Value;
        var version = m.Groups[2].Value;
        return $"https://api.vrchat.cloud/api/1/image/{fileId}/{version}/{size}";
    }
}

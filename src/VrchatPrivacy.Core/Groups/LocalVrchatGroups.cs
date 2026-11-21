using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;

namespace VrchatPrivacy.Core.Groups;

/// <summary>
/// Reads the user's group order from VRChat's Unity PlayerPrefs in the Windows registry
/// (<c>HKCU\Software\VRChat\VRChat</c> keys <c>VRC_GROUP_ORDER_usr_…</c>).
/// Full names / icons / banners are not stored there - enrich via API.
/// </summary>
public static class LocalVrchatGroups
{
    // Registry APIs are Windows-only; callers on other OSes get an empty list.
    private const string RegistryPath = @"Software\VRChat\VRChat";
    private const string GroupOrderPrefix = "VRC_GROUP_ORDER_";

    /// <summary>
    /// Group IDs in the order VRChat shows them in the client, for the given user
    /// (or the first order key found if <paramref name="userId"/> is null).
    /// </summary>
    public static IReadOnlyList<string> ReadGroupOrder(string? userId = null)
    {
        if (!OperatingSystem.IsWindows())
        {
            return Array.Empty<string>();
        }

        return ReadGroupOrderWindows(userId);
    }

    [SupportedOSPlatform("windows")]
    private static IReadOnlyList<string> ReadGroupOrderWindows(string? userId)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryPath);
            if (key is null)
            {
                return Array.Empty<string>();
            }

            string? match = null;
            foreach (var name in key.GetValueNames())
            {
                if (!name.StartsWith(GroupOrderPrefix, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(userId) &&
                    !name.StartsWith(GroupOrderPrefix + userId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                match = name;
                break;
            }

            if (match is null)
            {
                return Array.Empty<string>();
            }

            var raw = ReadUnityString(key, match);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return Array.Empty<string>();
            }

            using var doc = JsonDocument.Parse(raw.TrimEnd('\0'));
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<string>();
            }

            var list = new List<string>();
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                var id = el.GetString();
                if (!string.IsNullOrWhiteSpace(id) && id.StartsWith("grp_", StringComparison.OrdinalIgnoreCase))
                {
                    list.Add(id);
                }
            }

            return list;
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    [SupportedOSPlatform("windows")]
    private static string? ReadUnityString(RegistryKey key, string valueName)
    {
        var value = key.GetValue(valueName);
        return value switch
        {
            string s => s,
            byte[] bytes => Encoding.UTF8.GetString(bytes).TrimEnd('\0'),
            _ => value?.ToString(),
        };
    }
}

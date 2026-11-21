using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using VrchatPrivacy.Core.Settings;

namespace VrchatPrivacy.Core.Auth;

[SupportedOSPlatform("windows")]
public sealed class DpapiCredentialStore : ICredentialStore
{
    private readonly string _path;

    public DpapiCredentialStore(string? directory = null)
    {
        AppDataPaths.MigrateFromLegacyIfNeeded();
        var root = directory ?? AppDataPaths.Root;
        Directory.CreateDirectory(root);
        _path = Path.Combine(root, "auth.bin");
    }

    public Task SaveAuthCookiesAsync(string authCookie, string? twoFactorAuthCookie, CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.Serialize(new CookiePayload(authCookie, twoFactorAuthCookie));
        var bytes = Encoding.UTF8.GetBytes(payload);
        var protectedBytes = ProtectedData.Protect(bytes, optionalEntropy: null, scope: DataProtectionScope.CurrentUser);
        File.WriteAllBytes(_path, protectedBytes);
        return Task.CompletedTask;
    }

    public Task<(string? AuthCookie, string? TwoFactorAuthCookie)> LoadAuthCookiesAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_path))
        {
            return Task.FromResult<(string?, string?)>((null, null));
        }

        var protectedBytes = File.ReadAllBytes(_path);
        var bytes = ProtectedData.Unprotect(protectedBytes, optionalEntropy: null, scope: DataProtectionScope.CurrentUser);
        var payload = JsonSerializer.Deserialize<CookiePayload>(Encoding.UTF8.GetString(bytes));
        return Task.FromResult((payload?.Auth, payload?.TwoFactorAuth));
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        if (File.Exists(_path))
        {
            File.Delete(_path);
        }

        return Task.CompletedTask;
    }

    private sealed record CookiePayload(string Auth, string? TwoFactorAuth);
}

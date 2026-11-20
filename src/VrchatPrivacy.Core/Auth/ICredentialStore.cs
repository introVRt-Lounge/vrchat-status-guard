namespace VrchatPrivacy.Core.Auth;

/// <summary>
/// Stores reusable VRChat session material using OS protection (DPAPI on Windows).
/// Implementations must never log passwords, OTP codes, or cookie values.
/// </summary>
public interface ICredentialStore
{
    Task SaveAuthCookiesAsync(string authCookie, string? twoFactorAuthCookie, CancellationToken cancellationToken = default);

    Task<(string? AuthCookie, string? TwoFactorAuthCookie)> LoadAuthCookiesAsync(CancellationToken cancellationToken = default);

    Task ClearAsync(CancellationToken cancellationToken = default);
}

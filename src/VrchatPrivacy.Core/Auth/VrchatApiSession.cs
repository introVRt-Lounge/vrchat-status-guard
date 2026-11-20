using System.Net;
using VRChat.API.Client;
using VRChat.API.Model;
using VrchatPrivacy.Core.Protection;

namespace VrchatPrivacy.Core.Auth;

public sealed class VrchatApiSession : IVrchatSession, IDisposable
{
    public const string ApplicationName = "VrchatPrivacyGuard";
    public const string ApplicationVersion = "0.1.0";
    public const string ApplicationContact = "privacy-guard@introvrtlounge.com";

    private readonly ICredentialStore _store;
    private readonly TimeSpan _minMutationInterval;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IVRChat? _client;
    private DateTimeOffset _lastMutationUtc = DateTimeOffset.MinValue;
    private string? _userId;
    private string? _displayName;

    public VrchatApiSession(ICredentialStore store, TimeSpan? minMutationInterval = null)
    {
        _store = store;
        _minMutationInterval = minMutationInterval ?? TimeSpan.FromSeconds(60);
    }

    public bool IsAuthenticated => _client is not null && !string.IsNullOrEmpty(_userId);

    public async Task<LoginChallenge?> BeginLoginAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _client = new VRChatClientBuilder()
                .WithUsername(username)
                .WithPassword(password)
                .WithApplication(ApplicationName, ApplicationVersion, ApplicationContact)
                .Build();

            var response = await _client.Authentication.GetCurrentUserAsync(cancellationToken).ConfigureAwait(false);
            if (response.TryGet<RequiresTwoFactorAuth>(out var pending))
            {
                var methods = pending.VarRequiresTwoFactorAuth ?? new List<TwoFactorAuthType>();
                return new LoginChallenge(
                    RequiresTotp: methods.Contains(TwoFactorAuthType.Totp),
                    RequiresEmailOtp: methods.Contains(TwoFactorAuthType.EmailOtp));
            }

            if (response.TryGet<CurrentUser>(out var user) && user is not null)
            {
                await CaptureSessionAsync(user, cancellationToken).ConfigureAwait(false);
                return null;
            }

            throw new InvalidOperationException("Unexpected login response from VRChat.");
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task CompleteTotpAsync(string code, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureClient();
            await _client!.Authentication.Verify2FAAsync(new TwoFactorAuthCode(code), cancellationToken).ConfigureAwait(false);
            var user = await RequireCurrentUserAsync(cancellationToken).ConfigureAwait(false);
            await CaptureSessionAsync(user, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task CompleteEmailOtpAsync(string code, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureClient();
            await _client!.Authentication.Verify2FAEmailCodeAsync(new TwoFactorEmailCode(code), cancellationToken).ConfigureAwait(false);
            var user = await RequireCurrentUserAsync(cancellationToken).ConfigureAwait(false);
            await CaptureSessionAsync(user, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> TryResumeFromStoreAsync(CancellationToken cancellationToken = default)
    {
        var (auth, twoFactor) = await _store.LoadAuthCookiesAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(auth))
        {
            return false;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var builder = new VRChatClientBuilder()
                .WithApplication(ApplicationName, ApplicationVersion, ApplicationContact)
                .WithAuthCookie(auth, twoFactor ?? string.Empty);
            _client = builder.Build();

            try
            {
                var user = await RequireCurrentUserAsync(cancellationToken).ConfigureAwait(false);
                _userId = user.Id;
                _displayName = user.DisplayName;
                return true;
            }
            catch
            {
                _client = null;
                _userId = null;
                _displayName = null;
                await _store.ClearAsync(cancellationToken).ConfigureAwait(false);
                return false;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<SessionSnapshot> GetCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureClient();
            var user = await RequireCurrentUserAsync(cancellationToken).ConfigureAwait(false);
            _userId = user.Id;
            _displayName = user.DisplayName;
            var status = MapStatusToApi(user.Status);
            return new SessionSnapshot(user.Id, user.DisplayName, status);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ApplyProtectionStatusAsync(ProtectionStatus status, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureClient();
            if (string.IsNullOrEmpty(_userId))
            {
                var user = await RequireCurrentUserAsync(cancellationToken).ConfigureAwait(false);
                _userId = user.Id;
            }

            await WaitForMutationBudgetAsync(cancellationToken).ConfigureAwait(false);

            var apiStatus = ProtectionStatusMapping.ToApiStatus(status);
            var request = new UpdateUserRequest
            {
                Status = ParseUserStatus(apiStatus),
            };

            await _client!.Users.UpdateUserAsync(_userId!, request, cancellationToken).ConfigureAwait(false);
            _lastMutationUtc = DateTimeOffset.UtcNow;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
    {
        _gate.Dispose();
    }

    private async Task CaptureSessionAsync(CurrentUser user, CancellationToken cancellationToken)
    {
        _userId = user.Id;
        _displayName = user.DisplayName;
        var cookies = _client!.GetCookies();
        var auth = cookies.FirstOrDefault(c => c.Name.Equals("auth", StringComparison.OrdinalIgnoreCase))?.Value;
        var twoFactor = cookies.FirstOrDefault(c => c.Name.Equals("twoFactorAuth", StringComparison.OrdinalIgnoreCase))?.Value;
        if (!string.IsNullOrEmpty(auth))
        {
            await _store.SaveAuthCookiesAsync(auth, twoFactor, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<CurrentUser> RequireCurrentUserAsync(CancellationToken cancellationToken)
    {
        var response = await _client!.Authentication.GetCurrentUserAsync(cancellationToken).ConfigureAwait(false);
        if (response.TryGet<CurrentUser>(out var user) && user is not null)
        {
            return user;
        }

        throw new InvalidOperationException("Not authenticated with VRChat.");
    }

    private void EnsureClient()
    {
        if (_client is null)
        {
            throw new InvalidOperationException("VRChat session has not been started.");
        }
    }

    private async Task WaitForMutationBudgetAsync(CancellationToken cancellationToken)
    {
        var elapsed = DateTimeOffset.UtcNow - _lastMutationUtc;
        if (elapsed < _minMutationInterval)
        {
            var delay = _minMutationInterval - elapsed;
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }
    }

    private static UserStatus ParseUserStatus(string apiStatus) => apiStatus switch
    {
        "ask me" => UserStatus.AskMe,
        "busy" => UserStatus.Busy,
        "join me" => UserStatus.JoinMe,
        "active" => UserStatus.Active,
        "offline" => UserStatus.Offline,
        _ => UserStatus.AskMe,
    };

    private static string MapStatusToApi(UserStatus status) => status switch
    {
        UserStatus.AskMe => "ask me",
        UserStatus.Busy => "busy",
        UserStatus.JoinMe => "join me",
        UserStatus.Active => "active",
        UserStatus.Offline => "offline",
        _ => "unknown",
    };
}


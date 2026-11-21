using System.Net;
using VRChat.API.Client;
using VRChat.API.Model;
using VrchatPrivacy.Core.Groups;
using VrchatPrivacy.Core.Logging;
using VrchatPrivacy.Core.Protection;
using VrchatPrivacy.Core.Worlds;

namespace VrchatPrivacy.Core.Auth;

public sealed class VrchatApiSession : IVrchatSession, IDisposable
{
    public const string ApplicationName = "VrchatStatusGuard";
    public const string ApplicationVersion = "0.1.0";
    public const string ApplicationContact = "status-guard@introvrtlounge.com";

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
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            AppLog.Info("BeginLogin: building client");
            _client = BuildClient(username, password);

            var http = await _client.Authentication.GetCurrentUserWithHttpInfoAsync(cancellationToken)
                .ConfigureAwait(false);
            var response = http.Data;
            AppLog.Info(
                $"BeginLogin: HTTP {(int)http.StatusCode} type={response?.ActualInstance?.GetType().Name ?? "null"} schema={response?.SchemaType} cookies={http.Cookies?.Count ?? 0}");

            // Persist the pending auth cookie from the password step so Verify2FA has credentials.
            await RebuildClientFromCookiesAsync(http.Cookies, cancellationToken).ConfigureAwait(false);

            if (response is null)
            {
                var raw = http.RawContent ?? string.Empty;
                if (raw.Contains("requiresTwoFactorAuth", StringComparison.OrdinalIgnoreCase))
                {
                    AppLog.Info("BeginLogin: 2FA required (from raw body; Data was null)");
                    return new LoginChallenge(RequiresTotp: true, RequiresEmailOtp: raw.Contains("emailOtp", StringComparison.OrdinalIgnoreCase));
                }

                throw new InvalidOperationException("VRChat returned an empty login response.");
            }

            if (TryExtractTwoFactor(response, out var challenge))
            {
                AppLog.Info($"BeginLogin: 2FA required totp={challenge!.RequiresTotp} email={challenge.RequiresEmailOtp}");
                return challenge;
            }

            if (TryExtractUser(response, out var user))
            {
                await CaptureSessionAsync(user!, cancellationToken).ConfigureAwait(false);
                AppLog.Info($"BeginLogin: logged in without 2FA as {user!.DisplayName}");
                return null;
            }

            throw new InvalidOperationException(
                $"Unexpected login response from VRChat ({response.ActualInstance?.GetType().Name ?? "null"}).");
        }
        catch (Exception ex)
        {
            AppLog.Error("BeginLogin failed", ex);
            throw;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task CompleteTotpAsync(string code, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureClient();
            AppLog.Info("CompleteTotp: verifying");
            var http = await _client!.Authentication
                .Verify2FAWithHttpInfoAsync(new TwoFactorAuthCode(code.Trim()), cancellationToken)
                .ConfigureAwait(false);
            AppLog.Info(
                $"CompleteTotp: HTTP {(int)http.StatusCode} verified={http.Data?.Verified} cookies={http.Cookies?.Count ?? 0}");
            if (http.Data is { Verified: false })
            {
                throw new InvalidOperationException("VRChat rejected the authenticator code.");
            }

            await RebuildClientFromCookiesAsync(http.Cookies, cancellationToken).ConfigureAwait(false);

            AppLog.Info("CompleteTotp: fetching user");
            var user = await RequireCurrentUserAsync(cancellationToken).ConfigureAwait(false);
            await CaptureSessionAsync(user, cancellationToken).ConfigureAwait(false);
            AppLog.Info($"CompleteTotp: session captured for {user.DisplayName}");
        }
        catch (Exception ex)
        {
            AppLog.Error("CompleteTotp failed", ex);
            throw;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task CompleteEmailOtpAsync(string code, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureClient();
            AppLog.Info("CompleteEmailOtp: verifying");
            var http = await _client!.Authentication
                .Verify2FAEmailCodeWithHttpInfoAsync(new TwoFactorEmailCode(code.Trim()), cancellationToken)
                .ConfigureAwait(false);
            AppLog.Info(
                $"CompleteEmailOtp: HTTP {(int)http.StatusCode} verified={http.Data?.Verified} cookies={http.Cookies?.Count ?? 0}");
            if (http.Data is { Verified: false })
            {
                throw new InvalidOperationException("VRChat rejected the email verification code.");
            }

            await RebuildClientFromCookiesAsync(http.Cookies, cancellationToken).ConfigureAwait(false);

            AppLog.Info("CompleteEmailOtp: fetching user");
            var user = await RequireCurrentUserAsync(cancellationToken).ConfigureAwait(false);
            await CaptureSessionAsync(user, cancellationToken).ConfigureAwait(false);
            AppLog.Info($"CompleteEmailOtp: session captured for {user.DisplayName}");
        }
        catch (Exception ex)
        {
            AppLog.Error("CompleteEmailOtp failed", ex);
            throw;
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
            AppLog.Info("TryResume: no auth.bin");
            return false;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            AppLog.Info("TryResume: restoring cookies");
            var builder = new VRChatClientBuilder()
                .WithApplication(ApplicationName, ApplicationVersion, ApplicationContact)
                .WithAuthCookie(auth, twoFactor ?? string.Empty);
            _client = builder.Build();

            try
            {
                var user = await RequireCurrentUserAsync(cancellationToken).ConfigureAwait(false);
                _userId = user.Id;
                _displayName = user.DisplayName;
                AppLog.Info($"TryResume: ok as {user.DisplayName}");
                return true;
            }
            catch (Exception ex)
            {
                AppLog.Error("TryResume: cookie session invalid", ex);
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
            return new SessionSnapshot(user.Id, user.DisplayName ?? string.Empty, status);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ApplyProtectionStatusAsync(ProtectionStatus status, CancellationToken cancellationToken = default)
    {
        // Rate-limit wait stays OUTSIDE the API gate so catalog reads are not starved for 60s.
        await WaitForMutationBudgetAsync(cancellationToken).ConfigureAwait(false);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureClient();
            if (string.IsNullOrEmpty(_userId))
            {
                var user = await RequireCurrentUserAsync(cancellationToken).ConfigureAwait(false);
                _userId = user.Id;
            }

            var apiStatus = ProtectionStatusMapping.ToApiStatus(status);
            AppLog.Info($"ApplyStatus: {_userId} -> {apiStatus}");
            var request = new UpdateUserRequest
            {
                Status = ParseUserStatus(apiStatus),
            };

            await _client!.Users.UpdateUserAsync(_userId!, request, cancellationToken).ConfigureAwait(false);
            _lastMutationUtc = DateTimeOffset.UtcNow;
            AppLog.Info("ApplyStatus: ok");
        }
        catch (Exception ex)
        {
            AppLog.Error("ApplyStatus failed", ex);
            throw;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<WorldInfo>> GetRecentWorldsAsync(int count = 20, CancellationToken cancellationToken = default)
    {
        if (!IsAuthenticated)
        {
            return Array.Empty<WorldInfo>();
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureClient();
            var n = Math.Clamp(count, 1, 60);
            AppLog.Info($"GetRecentWorlds: n={n}");
            var worlds = await _client!.Worlds.GetRecentWorldsAsync(n: n, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            var list = new List<WorldInfo>();
            foreach (var w in worlds ?? Enumerable.Empty<LimitedWorld>())
            {
                if (string.IsNullOrWhiteSpace(w.Id))
                {
                    continue;
                }

                list.Add(new WorldInfo(
                    w.Id,
                    string.IsNullOrWhiteSpace(w.Name) ? w.Id : w.Name,
                    string.IsNullOrWhiteSpace(w.ThumbnailImageUrl) ? w.ImageUrl : w.ThumbnailImageUrl,
                    w.AuthorName));
            }

            AppLog.Info($"GetRecentWorlds: {list.Count} worlds");
            return list;
        }
        catch (Exception ex)
        {
            AppLog.Error("GetRecentWorlds failed", ex);
            return Array.Empty<WorldInfo>();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<GroupInfo>> GetUserGroupsAsync(CancellationToken cancellationToken = default)
    {
        if (!IsAuthenticated)
        {
            return Array.Empty<GroupInfo>();
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureClient();
            if (string.IsNullOrEmpty(_userId))
            {
                var user = await RequireCurrentUserAsync(cancellationToken).ConfigureAwait(false);
                _userId = user.Id;
            }

            AppLog.Info($"GetUserGroups: user={_userId}");
            var raw = await _client!.Users.GetUserGroupsAsync(_userId!, cancellationToken).ConfigureAwait(false);
            var byId = new Dictionary<string, GroupInfo>(StringComparer.OrdinalIgnoreCase);
            foreach (var g in raw ?? Enumerable.Empty<LimitedUserGroups>())
            {
                // LimitedUserGroups.Id is usually membership (gmem_); GroupId is the real group.
                var id = !string.IsNullOrWhiteSpace(g.GroupId) ? g.GroupId : g.Id;
                if (string.IsNullOrWhiteSpace(id) || !id.StartsWith("grp_", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                byId[id] = new GroupInfo(
                    id,
                    string.IsNullOrWhiteSpace(g.Name) ? id : g.Name,
                    g.ShortCode,
                    g.Discriminator,
                    g.IconUrl,
                    g.BannerUrl,
                    g.IsRepresenting);
            }

            var ordered = new List<GroupInfo>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var id in LocalVrchatGroups.ReadGroupOrder(_userId))
            {
                if (byId.TryGetValue(id, out var info) && seen.Add(id))
                {
                    ordered.Add(info);
                }
                else if (seen.Add(id))
                {
                    // On disk but API missed it (privacy / lag) — still show id so user can pick.
                    ordered.Add(new GroupInfo(id, id, null, null, null, null, false));
                }
            }

            foreach (var info in byId.Values.OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase))
            {
                if (seen.Add(info.Id))
                {
                    ordered.Add(info);
                }
            }

            AppLog.Info($"GetUserGroups: {ordered.Count} groups");
            return ordered;
        }
        catch (Exception ex)
        {
            AppLog.Error("GetUserGroups failed", ex);
            return Array.Empty<GroupInfo>();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SetRepresentingGroupAsync(string groupId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);
        if (!groupId.StartsWith("grp_", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Group id must start with grp_.", nameof(groupId));
        }

        await WaitForMutationBudgetAsync(cancellationToken).ConfigureAwait(false);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureClient();
            AppLog.Info($"SetRepresentingGroup: {groupId}");
            await _client!.Groups.UpdateGroupRepresentationAsync(
                    groupId,
                    new UpdateGroupRepresentationRequest(isRepresenting: true),
                    cancellationToken)
                .ConfigureAwait(false);
            _lastMutationUtc = DateTimeOffset.UtcNow;
            AppLog.Info("SetRepresentingGroup: ok");
        }
        catch (Exception ex)
        {
            AppLog.Error("SetRepresentingGroup failed", ex);
            throw;
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

    private static IVRChat BuildClient(string username, string password) =>
        new VRChatClientBuilder()
            .WithUsername(username)
            .WithPassword(password)
            .WithApplication(ApplicationName, ApplicationVersion, ApplicationContact)
            .Build();

    private async Task CaptureSessionAsync(CurrentUser user, CancellationToken cancellationToken)
    {
        _userId = user.Id ?? throw new InvalidOperationException("VRChat user id was missing after login.");
        _displayName = user.DisplayName;

        var (auth, twoFactor, names) = ReadAuthCookies();
        AppLog.Info($"CaptureSession: cookies=[{string.Join(",", names)}] auth={(auth is null ? "missing" : "present")} 2fa={(twoFactor is null ? "missing" : "present")}");

        if (!string.IsNullOrEmpty(auth))
        {
            await _store.SaveAuthCookiesAsync(auth, twoFactor, cancellationToken).ConfigureAwait(false);
            AppLog.Info("CaptureSession: auth.bin saved");
        }
        else
        {
            AppLog.Warn("CaptureSession: no auth cookie to persist");
        }
    }

    private async Task RebuildClientFromCookiesAsync(List<Cookie>? responseCookies, CancellationToken cancellationToken)
    {
        var (auth, twoFactor, names) = ReadAuthCookies(responseCookies);
        AppLog.Info($"RebuildClient: jar=[{string.Join(",", names)}] auth={(auth is null ? "missing" : "present")} 2fa={(twoFactor is null ? "missing" : "present")}");
        if (string.IsNullOrWhiteSpace(auth))
        {
            AppLog.Warn("RebuildClient: no auth cookie available; continuing with existing client");
            return;
        }

        _client = new VRChatClientBuilder()
            .WithApplication(ApplicationName, ApplicationVersion, ApplicationContact)
            .WithAuthCookie(auth, twoFactor ?? string.Empty)
            .Build();
        await _store.SaveAuthCookiesAsync(auth, twoFactor, cancellationToken).ConfigureAwait(false);
        AppLog.Info("RebuildClient: client rebuilt from auth cookies");
    }

    private (string? Auth, string? TwoFactor, List<string> Names) ReadAuthCookies(IEnumerable<Cookie>? extra = null)
    {
        var names = new List<string>();
        string? auth = null;
        string? twoFactor = null;

        void Consider(Cookie? cookie)
        {
            if (cookie?.Name is null)
            {
                return;
            }

            names.Add(cookie.Name);
            if (string.Equals(cookie.Name, "auth", StringComparison.OrdinalIgnoreCase))
            {
                auth = cookie.Value;
            }
            else if (string.Equals(cookie.Name, "twoFactorAuth", StringComparison.OrdinalIgnoreCase))
            {
                twoFactor = cookie.Value;
            }
        }

        try
        {
            foreach (var cookie in _client?.GetCookies() ?? Enumerable.Empty<Cookie>())
            {
                Consider(cookie);
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn($"ReadAuthCookies jar failed: {ex.Message}");
        }

        if (extra is not null)
        {
            foreach (var cookie in extra)
            {
                Consider(cookie);
            }
        }

        return (auth, twoFactor, names.Distinct(StringComparer.OrdinalIgnoreCase).ToList());
    }

    private async Task<CurrentUser> RequireCurrentUserAsync(CancellationToken cancellationToken)
    {
        var http = await _client!.Authentication.GetCurrentUserWithHttpInfoAsync(cancellationToken)
            .ConfigureAwait(false);

        var raw = http.RawContent ?? string.Empty;
        var prefix = raw.Length <= 160 ? raw : raw[..160];
        AppLog.Info(
            $"GetCurrentUser: HTTP {(int)http.StatusCode} type={http.Data?.ActualInstance?.GetType().Name ?? "null"} schema={http.Data?.SchemaType} rawLen={raw.Length} cookies={http.Cookies?.Count ?? 0} prefix={prefix.Replace('\n', ' ')}");

        if (http.Data is not null && TryExtractUser(http.Data, out var user))
        {
            return user!;
        }

        // oneOf deserializer sometimes yields null Data even when body is a CurrentUser JSON.
        if (raw.Contains("\"displayName\"", StringComparison.Ordinal) ||
            raw.Contains("\"currentAvatar\"", StringComparison.Ordinal))
        {
            var direct = Newtonsoft.Json.JsonConvert.DeserializeObject<CurrentUser>(raw);
            if (direct?.Id is not null)
            {
                AppLog.Info($"GetCurrentUser: recovered CurrentUser via raw JSON ({direct.DisplayName})");
                return direct;
            }
        }

        if (http.Data is not null && TryExtractTwoFactor(http.Data, out _))
        {
            throw new InvalidOperationException("VRChat still requires two-factor authentication.");
        }

        if (raw.Contains("requiresTwoFactorAuth", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("VRChat still requires two-factor authentication.");
        }

        throw new InvalidOperationException(
            $"Not authenticated with VRChat (HTTP {(int)http.StatusCode}, empty/unparsed user).");
    }

    private static bool TryExtractUser(CurrentUserLoginResponse response, out CurrentUser? user)
    {
        if (response.TryGet<CurrentUser>(out var viaTryGet) && viaTryGet is not null)
        {
            user = viaTryGet;
            return true;
        }

        if (response.ActualInstance is CurrentUser viaInstance)
        {
            user = viaInstance;
            return true;
        }

        if (response.Value is CurrentUser viaValue)
        {
            user = viaValue;
            return true;
        }

        user = null;
        return false;
    }

    private static bool TryExtractTwoFactor(CurrentUserLoginResponse response, out LoginChallenge? challenge)
    {
        List<TwoFactorAuthType>? methods = null;
        if (response.TryGet<RequiresTwoFactorAuth>(out var pending) && pending is not null)
        {
            methods = pending.VarRequiresTwoFactorAuth;
        }
        else if (response.ActualInstance is RequiresTwoFactorAuth viaInstance)
        {
            methods = viaInstance.VarRequiresTwoFactorAuth;
        }

        if (methods is null)
        {
            challenge = null;
            return false;
        }

        challenge = new LoginChallenge(
            RequiresTotp: methods.Contains(TwoFactorAuthType.Totp) || methods.Contains(TwoFactorAuthType.Otp),
            RequiresEmailOtp: methods.Contains(TwoFactorAuthType.EmailOtp));
        return true;
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

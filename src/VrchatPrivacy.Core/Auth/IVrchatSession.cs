using VrchatPrivacy.Core.Protection;

namespace VrchatPrivacy.Core.Auth;

public sealed record LoginChallenge(bool RequiresTotp, bool RequiresEmailOtp);

public sealed record SessionSnapshot(string UserId, string DisplayName, string CurrentApiStatus);

/// <summary>
/// Authentication and status updates. Rate-limit callers to at most one mutating API call per 60 seconds
/// (VRChat community guidance for unofficial API use).
/// </summary>
public interface IVrchatSession
{
    Task<LoginChallenge?> BeginLoginAsync(string username, string password, CancellationToken cancellationToken = default);

    Task CompleteTotpAsync(string code, CancellationToken cancellationToken = default);

    Task CompleteEmailOtpAsync(string code, CancellationToken cancellationToken = default);

    Task<bool> TryResumeFromStoreAsync(CancellationToken cancellationToken = default);

    Task<SessionSnapshot> GetCurrentUserAsync(CancellationToken cancellationToken = default);

    Task ApplyProtectionStatusAsync(ProtectionStatus status, CancellationToken cancellationToken = default);
}

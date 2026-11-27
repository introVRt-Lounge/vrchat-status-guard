using StatusGuard.Core.Groups;
using StatusGuard.Core.Protection;
using StatusGuard.Core.Worlds;

namespace StatusGuard.Core.Auth;

public sealed record LoginChallenge(bool RequiresTotp, bool RequiresEmailOtp);

public sealed record SessionSnapshot(string UserId, string DisplayName, string CurrentApiStatus);

/// <summary>
/// Authentication and status updates. Rate-limit callers to at most one mutating API call per 60 seconds
/// (VRChat community guidance for unofficial API use).
/// </summary>
public interface IVrchatSession
{
    bool IsAuthenticated { get; }

    Task<LoginChallenge?> BeginLoginAsync(string username, string password, CancellationToken cancellationToken = default);

    Task CompleteTotpAsync(string code, CancellationToken cancellationToken = default);

    Task CompleteEmailOtpAsync(string code, CancellationToken cancellationToken = default);

    Task<bool> TryResumeFromStoreAsync(CancellationToken cancellationToken = default);

    /// <summary>Drop in-memory client and wipe stored session cookies (DPAPI auth.bin).</summary>
    Task LogoutAsync(CancellationToken cancellationToken = default);

    Task<SessionSnapshot> GetCurrentUserAsync(CancellationToken cancellationToken = default);

    Task ApplyProtectionStatusAsync(ProtectionStatus status, CancellationToken cancellationToken = default);

    /// <summary>Recently visited worlds for the logged-in user (VRChat /worlds/recent).</summary>
    Task<IReadOnlyList<WorldInfo>> GetRecentWorldsAsync(int count = 20, CancellationToken cancellationToken = default);

    /// <summary>
    /// Groups the logged-in user belongs to (names, short codes, icon/banner URLs).
    /// Prefer ordering from <see cref="LocalVrchatGroups.ReadGroupOrder"/> when available.
    /// </summary>
    Task<IReadOnlyList<GroupInfo>> GetUserGroupsAsync(CancellationToken cancellationToken = default);

    /// <summary>Set which group the user is representing (nameplate). Rate-limited mutation.</summary>
    Task SetRepresentingGroupAsync(string groupId, CancellationToken cancellationToken = default);
}

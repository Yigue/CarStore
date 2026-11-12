using Domain.Users;

namespace Application.Abstractions.Authentication;

public interface ITokenProvider
{
    /// <summary>Full session token scoped to <paramref name="dealerId"/> — one of the user's effective
    /// memberships (sfc B6.3 / RN-043). The caller resolves and validates <paramref name="dealerId"/>
    /// before calling this; the provider does not re-derive it from <c>user.DealerId</c>.</summary>
    string Create(User user, Guid dealerId, string roleName);

    /// <summary>
    /// sfc B6.3 / RN-043: short-lived token for a user with 2+ dealers, issued instead of a session token.
    /// Carries only <c>sub</c> and <c>login_stage</c> — no <c>dealer_id</c>, no <c>role</c> — so it
    /// authenticates the caller (UserId is known) but cannot act as any tenant. Redeemed only by
    /// <c>POST auth/select-dealer</c>.
    /// </summary>
    string CreatePendingDealerSelection(User user);
}

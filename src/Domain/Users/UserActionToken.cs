using System.Text.RegularExpressions;
using SharedKernel;

namespace Domain.Users;

/// <summary>
/// sfc A4.1 / RN-030 (design C16): single-use, expiring token emailed to a person. Only the SHA-256 digest
/// of the secret is stored (<see cref="TokenHash"/>), never the secret itself.
/// Single use is enforced in the database by an atomic <c>UPDATE ... WHERE consumed_at IS NULL</c>
/// (rowcount = 1); <see cref="TryConsume"/> is the in-memory mirror of that rule. Issuing a new token
/// revokes the pending ones of the same <c>(purpose, email)</c>. A revoked token is marked consumed.
/// Not tenant-filtered: it is looked up anonymously by hash, like <see cref="PasswordResetToken"/>.
/// </summary>
public sealed partial class UserActionToken : Entity
{
    public UserActionPurpose Purpose { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public Guid? UserId { get; private set; }
    public Guid? RoleId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? ConsumedAtUtc { get; private set; }
    public Guid? CreatedBy { get; private set; }

    private UserActionToken()
    {
    }

    public static TimeSpan LifetimeOf(UserActionPurpose purpose) => purpose switch
    {
        UserActionPurpose.EmailVerification => TimeSpan.FromHours(24),
        UserActionPurpose.Invitation => TimeSpan.FromDays(7),
        UserActionPurpose.OwnerTransfer => TimeSpan.FromHours(48),
        _ => throw new DomainException($"Unknown token purpose {purpose}")
    };

    public static UserActionToken Issue(
        IEnumerable<UserActionToken> existing,
        UserActionPurpose purpose,
        Guid dealerId,
        string email,
        string tokenHash,
        Guid? userId,
        Guid? roleId,
        Guid? createdBy,
        DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new DomainException("Email cannot be empty");
        if (!Sha256Pattern().IsMatch(tokenHash ?? string.Empty))
            throw new DomainException("TokenHash must be a 64-character hex SHA-256 digest");

        foreach (UserActionToken previous in existing)
        {
            if (previous.Purpose == purpose
                && string.Equals(previous.Email, email, StringComparison.OrdinalIgnoreCase)
                && previous.IsPending(nowUtc))
            {
                previous.Revoke(nowUtc);
            }
        }

        var token = new UserActionToken
        {
            Id = Guid.NewGuid(),
            Purpose = purpose,
            TokenHash = tokenHash,
            Email = email,
            UserId = userId,
            RoleId = roleId,
            CreatedBy = createdBy,
            CreatedAtUtc = nowUtc,
            ExpiresAtUtc = nowUtc + LifetimeOf(purpose)
        };
        token.SetDealer(dealerId);
        return token;
    }

    public bool IsPending(DateTime nowUtc) => ConsumedAtUtc is null && ExpiresAtUtc > nowUtc;

    /// <summary>In-memory mirror of the atomic database consume: true exactly once, never after expiry.</summary>
    public bool TryConsume(DateTime nowUtc)
    {
        if (!IsPending(nowUtc))
            return false;

        ConsumedAtUtc = nowUtc;
        return true;
    }

    public void Revoke(DateTime nowUtc)
    {
        ConsumedAtUtc ??= nowUtc;
    }

    [GeneratedRegex("^[0-9a-fA-F]{64}$")]
    private static partial Regex Sha256Pattern();
}

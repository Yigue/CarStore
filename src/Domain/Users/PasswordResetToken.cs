namespace Domain.Users;

/// <summary>
/// Single-use, time-limited token issued when a user requests a password reset.
/// Persisted in <c>password_reset_tokens</c>. The token string is the value
/// emailed to the user (embedded in the reset link); it is looked up directly,
/// so the entity is intentionally not tenant-scoped.
/// </summary>
public sealed class PasswordResetToken
{
    private PasswordResetToken()
    {
    }

    public PasswordResetToken(Guid userId, string token, DateTime expiresAt)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        Token = token;
        ExpiresAt = expiresAt;
        CreatedAt = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Token { get; private set; } = string.Empty;
    public DateTime ExpiresAt { get; private set; }
    public DateTime? UsedAt { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public bool IsUsable(DateTime nowUtc) => UsedAt is null && ExpiresAt > nowUtc;

    public void MarkUsed(DateTime usedAtUtc)
    {
        UsedAt = usedAtUtc;
    }
}

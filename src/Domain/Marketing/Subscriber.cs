using System.Text.RegularExpressions;
using SharedKernel;

namespace Domain.Marketing;

public enum SubscriberStatus
{
    Pending = 0,
    Confirmed = 1,
    Unsubscribed = 2
}

/// <summary>
/// sfc B5.1 / RN-019: a newsletter contact of one dealer, unique per <c>(dealer_id, email_normalized)</c>. It is not a
/// <c>Client</c>. Double opt-in: it starts <see cref="SubscriberStatus.Pending"/> and only the emailed confirmation link
/// makes it <see cref="SubscriberStatus.Confirmed"/>. Unsubscribing is one click (RFC 8058). Like
/// <c>UserActionToken</c>, only SHA-256 digests of the emailed secrets are stored.
/// </summary>
public sealed partial class Subscriber : Entity
{
    public const int MaxEmailLength = 254;
    public static readonly TimeSpan ConfirmationLifetime = TimeSpan.FromDays(7);

    public string Email { get; private set; } = string.Empty;
    public string EmailNormalized { get; private set; } = string.Empty;
    public SubscriberStatus Status { get; private set; }
    public string Source { get; private set; } = string.Empty;
    public string? ConfirmTokenHash { get; private set; }
    public DateTime? ConfirmTokenExpiresAtUtc { get; private set; }
    public string UnsubscribeTokenHash { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? ConfirmedAtUtc { get; private set; }
    public DateTime? UnsubscribedAtUtc { get; private set; }

    private Subscriber()
    {
    }

    public static Result<Subscriber> Subscribe(
        Guid dealerId, string email, string source, string confirmTokenHash, string unsubscribeTokenHash, DateTime nowUtc)
    {
        string trimmed = email?.Trim() ?? string.Empty;
        if (trimmed.Length == 0 || trimmed.Length > MaxEmailLength || !EmailPattern().IsMatch(trimmed))
        {
            return Result.Failure<Subscriber>(SubscriberErrors.InvalidEmail);
        }

        EnsureDigest(confirmTokenHash);
        EnsureDigest(unsubscribeTokenHash);

        var subscriber = new Subscriber
        {
            Id = Guid.NewGuid(),
            Email = trimmed,
            EmailNormalized = Normalize(trimmed),
            Status = SubscriberStatus.Pending,
            Source = string.IsNullOrWhiteSpace(source) ? "web" : source.Trim(),
            ConfirmTokenHash = confirmTokenHash,
            ConfirmTokenExpiresAtUtc = nowUtc + ConfirmationLifetime,
            UnsubscribeTokenHash = unsubscribeTokenHash,
            CreatedAtUtc = nowUtc
        };
        subscriber.SetDealer(dealerId);
        return subscriber;
    }

    /// <summary>Backfill of the legacy "Newsletter" clients (B5.1.4): they had already been receiving it.</summary>
    public static Subscriber ImportConfirmed(Guid dealerId, string email, string unsubscribeTokenHash, DateTime createdAtUtc)
    {
        EnsureDigest(unsubscribeTokenHash);
        var subscriber = new Subscriber
        {
            Id = Guid.NewGuid(),
            Email = email.Trim(),
            EmailNormalized = Normalize(email),
            Status = SubscriberStatus.Confirmed,
            Source = "legacy",
            UnsubscribeTokenHash = unsubscribeTokenHash,
            CreatedAtUtc = createdAtUtc,
            ConfirmedAtUtc = createdAtUtc
        };
        subscriber.SetDealer(dealerId);
        return subscriber;
    }

    public static string Normalize(string email) => email.Trim().ToLowerInvariant();

    /// <summary>True when this call confirmed the subscription; an expired or already used link confirms nothing.</summary>
    public bool Confirm(DateTime nowUtc)
    {
        if (Status != SubscriberStatus.Pending || ConfirmTokenHash is null || ConfirmTokenExpiresAtUtc <= nowUtc)
        {
            return false;
        }

        Status = SubscriberStatus.Confirmed;
        ConfirmedAtUtc = nowUtc;
        ConfirmTokenHash = null;
        ConfirmTokenExpiresAtUtc = null;
        return true;
    }

    public void Unsubscribe(DateTime nowUtc)
    {
        if (Status == SubscriberStatus.Unsubscribed)
        {
            return;
        }

        Status = SubscriberStatus.Unsubscribed;
        UnsubscribedAtUtc = nowUtc;
        ConfirmTokenHash = null;
        ConfirmTokenExpiresAtUtc = null;
    }

    /// <summary>
    /// A new subscribe request for an existing address. Pending or unsubscribed: a fresh confirmation is issued (true).
    /// Confirmed: nothing changes (false), so the caller sends no email.
    /// </summary>
    public bool Resubscribe(string confirmTokenHash, DateTime nowUtc)
    {
        if (Status == SubscriberStatus.Confirmed)
        {
            return false;
        }

        EnsureDigest(confirmTokenHash);
        Status = SubscriberStatus.Pending;
        ConfirmTokenHash = confirmTokenHash;
        ConfirmTokenExpiresAtUtc = nowUtc + ConfirmationLifetime;
        UnsubscribedAtUtc = null;
        return true;
    }

    public void RotateUnsubscribeToken(string unsubscribeTokenHash)
    {
        EnsureDigest(unsubscribeTokenHash);
        UnsubscribeTokenHash = unsubscribeTokenHash;
    }

    private static void EnsureDigest(string hash)
    {
        if (!Sha256Pattern().IsMatch(hash ?? string.Empty))
        {
            throw new DomainException("Token hash must be a 64-character hex SHA-256 digest");
        }
    }

    [GeneratedRegex("^[0-9A-Fa-f]{64}$")]
    private static partial Regex Sha256Pattern();

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailPattern();
}

public static class SubscriberErrors
{
    public static readonly Error InvalidEmail = Error.Validation("Newsletter.InvalidEmail", "Email no es válido.");
    public static readonly Error InvalidToken = Error.Validation("Newsletter.InvalidToken", "El enlace no es válido o ya venció.");
}

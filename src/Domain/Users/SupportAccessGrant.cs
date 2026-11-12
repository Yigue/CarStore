using SharedKernel;

namespace Domain.Users;

/// <summary>
/// sfc A4.3.5 / A19 / RN-032: Dealer authorizes read-only platform support access for N hours with audit trail.
/// Extends runbook D4 of spr without impersonation.
/// </summary>
public sealed class SupportAccessGrant : Entity
{
    private SupportAccessGrant()
    {
    }

    public Guid GrantedByUserId { get; private set; }
    public string SupportIdentifier { get; private set; } = string.Empty;
    public int DurationHours { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public DateTime GrantedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }
    public Guid? RevokedByUserId { get; private set; }

    public bool IsActive(DateTime nowUtc) =>
        RevokedAtUtc is null && nowUtc < ExpiresAtUtc;

    public static SupportAccessGrant Grant(
        Guid dealerId,
        Guid grantedByUserId,
        string supportIdentifier,
        int durationHours,
        string reason,
        DateTime nowUtc)
    {
        if (dealerId == Guid.Empty)
            throw new DomainException("DealerId cannot be empty");

        if (grantedByUserId == Guid.Empty)
            throw new DomainException("GrantedByUserId cannot be empty");

        if (string.IsNullOrWhiteSpace(supportIdentifier))
            throw new DomainException("SupportIdentifier cannot be empty");

        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException("Reason cannot be empty");

        if (durationHours <= 0 || durationHours > 72)
            throw new DomainException("DurationHours must be between 1 and 72 hours");

        var grant = new SupportAccessGrant
        {
            Id = Guid.NewGuid(),
            GrantedByUserId = grantedByUserId,
            SupportIdentifier = supportIdentifier.Trim(),
            DurationHours = durationHours,
            Reason = reason.Trim(),
            GrantedAtUtc = nowUtc,
            ExpiresAtUtc = nowUtc.AddHours(durationHours)
        };

        grant.SetDealer(dealerId);

        return grant;
    }

    public void Revoke(Guid revokedByUserId, DateTime nowUtc)
    {
        if (revokedByUserId == Guid.Empty)
            throw new DomainException("RevokedByUserId cannot be empty");

        if (RevokedAtUtc is not null)
            throw new DomainException("Support access grant is already revoked");

        RevokedAtUtc = nowUtc;
        RevokedByUserId = revokedByUserId;
    }
}

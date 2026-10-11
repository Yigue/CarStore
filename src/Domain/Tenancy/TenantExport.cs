using System.Text.RegularExpressions;
using SharedKernel;

namespace Domain.Tenancy;

/// <summary>
/// RN-014 (A7): an asynchronous export of every record of a dealer, delivered as a ZIP.
/// States: Requested, Running, Ready, then Expired or Failed. A Ready export is downloadable for
/// <see cref="RetentionDays"/> days; the expiry is also enforced on read, so correctness never
/// depends on the sweep job having run.
/// </summary>
public sealed partial class TenantExport : Entity
{
    public const int RetentionDays = 7;

    public Guid RequestedBy { get; private set; }
    public TenantExportStatus Status { get; private set; }
    public DateTime RequestedAtUtc { get; private set; }
    public DateTime? StartedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public string? ObjectKey { get; private set; }
    public string? Sha256 { get; private set; }
    public long? SizeBytes { get; private set; }
    public DateTime? ExpiresAt { get; private set; }
    public string? FailureReason { get; private set; }

    /// <summary>True while the export still occupies the single "active export" slot of the tenant.</summary>
    public bool IsActive => Status is TenantExportStatus.Requested or TenantExportStatus.Running;

    private TenantExport()
    {
    }

    public static TenantExport Request(Guid dealerId, Guid requestedBy, DateTime nowUtc)
    {
        if (requestedBy == Guid.Empty)
            throw new DomainException("RequestedBy cannot be empty");

        var export = new TenantExport
        {
            Id = Guid.NewGuid(),
            RequestedBy = requestedBy,
            Status = TenantExportStatus.Requested,
            RequestedAtUtc = nowUtc
        };
        export.SetDealer(dealerId);
        return export;
    }

    public void MarkRunning(DateTime nowUtc)
    {
        if (Status != TenantExportStatus.Requested)
            throw new DomainException($"Cannot start an export in state {Status}");

        Status = TenantExportStatus.Running;
        StartedAtUtc = nowUtc;
    }

    public void MarkReady(string objectKey, string sha256, long sizeBytes, DateTime nowUtc)
    {
        if (Status != TenantExportStatus.Running)
            throw new DomainException($"Cannot complete an export in state {Status}");
        if (string.IsNullOrWhiteSpace(objectKey))
            throw new DomainException("ObjectKey cannot be empty");
        if (!Sha256Pattern().IsMatch(sha256 ?? string.Empty))
            throw new DomainException("Sha256 must be a 64-character hex digest");

        Status = TenantExportStatus.Ready;
        ObjectKey = objectKey;
        Sha256 = sha256.ToLowerInvariant();
        SizeBytes = sizeBytes;
        CompletedAtUtc = nowUtc;
        ExpiresAt = nowUtc.AddDays(RetentionDays);
    }

    public void Expire(DateTime nowUtc)
    {
        if (Status != TenantExportStatus.Ready)
            throw new DomainException($"Cannot expire an export in state {Status}");

        Status = TenantExportStatus.Expired;
        CompletedAtUtc ??= nowUtc;
    }

    public void Fail(string reason, DateTime nowUtc)
    {
        if (!IsActive)
            throw new DomainException($"Cannot fail an export in state {Status}");

        Status = TenantExportStatus.Failed;
        FailureReason = reason.Length > 1000 ? reason[..1000] : reason;
        CompletedAtUtc = nowUtc;
    }

    [GeneratedRegex("^[0-9a-fA-F]{64}$")]
    private static partial Regex Sha256Pattern();
}

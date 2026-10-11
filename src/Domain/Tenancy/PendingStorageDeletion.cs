using SharedKernel;

namespace Domain.Tenancy;

/// <summary>
/// Durable retry list for storage objects that must be deleted but could not be (e.g. the object store was down
/// right after a tenant purge committed). Blob deletes cannot join a database transaction, so the key is parked here
/// instead of being lost. Deliberately NOT tenant-filtered and without a foreign key to the dealer.
/// </summary>
public sealed class PendingStorageDeletion : Entity
{
    private PendingStorageDeletion()
    {
    }

    public string ObjectKey { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }
    public int Attempts { get; private set; }
    public string? LastError { get; private set; }

    public static PendingStorageDeletion Create(Guid dealerId, string objectKey, DateTime createdAtUtc, string? error)
    {
        if (string.IsNullOrWhiteSpace(objectKey))
            throw new DomainException("ObjectKey cannot be empty");

        var pending = new PendingStorageDeletion
        {
            Id = Guid.NewGuid(),
            ObjectKey = objectKey,
            CreatedAtUtc = createdAtUtc,
            Attempts = 1,
            LastError = error is { Length: > MaxErrorLength } ? error[..MaxErrorLength] : error
        };
        pending.SetDealer(dealerId);
        return pending;
    }

    public const int MaxErrorLength = 1000;
}

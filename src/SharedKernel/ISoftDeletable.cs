namespace SharedKernel;

/// <summary>
/// Marks an entity as supporting logical (soft) deletion. Implementers keep the row in the
/// database and are excluded from default queries via an EF Core global query filter
/// (<c>!IsDeleted</c>), so the data can be recovered or audited instead of being lost.
/// </summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; }

    DateTime? DeletedAtUtc { get; }
}

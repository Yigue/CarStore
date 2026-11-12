namespace Application.Abstractions.Data;

/// <summary>
/// sfc B4.3 / RN-018 / ADR C7: atomic per-dealer, per-kind counter allocation (e.g. "quote"
/// folios). Relational providers (Postgres, SQLite) allocate via a single
/// <c>INSERT ... ON CONFLICT ... RETURNING</c> statement so concurrent callers never see the same
/// value and nothing is skipped on rollback. See Infrastructure.Common.DealerSequenceAllocator for
/// the InMemory test-provider fallback.
/// </summary>
public interface IDealerSequenceAllocator
{
    Task<long> NextAsync(Guid dealerId, string kind, CancellationToken cancellationToken = default);
}

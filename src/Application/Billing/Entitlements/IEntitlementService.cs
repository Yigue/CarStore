using SharedKernel;

namespace Application.Billing.Entitlements;

/// <summary>
/// sfc A3.1 (A1, A21, RN-011): enforces the plan's numeric limits before a new row of a tenant-scoped
/// resource (vehicles, users, webhooks) is created.
/// </summary>
public interface IEntitlementService
{
    /// <summary>
    /// Acquires a Postgres advisory lock scoped to (dealerId, resource) for the lifetime of the
    /// CALLER's ambient transaction, then checks the plan limit. The lock is NOT released by this
    /// call — pg_advisory_xact_lock releases automatically when the ambient transaction commits or
    /// rolls back. The caller MUST have already opened a transaction (via IApplicationDbContext's
    /// underlying DbContext, see ProvisionDealerCommandHandler for the IsRelational() guard pattern)
    /// BEFORE calling this, and must keep that transaction open until AFTER its own SaveChangesAsync
    /// that performs the actual insert — committing before the insert would release the lock too
    /// early and let a second concurrent request slip through the same check.
    /// On a non-relational provider (unit tests), the advisory lock call is skipped entirely (it's
    /// Postgres-only raw SQL) — correctness there relies on the test running single-threaded, which
    /// is why the real concurrency guarantee is only tested against real Postgres
    /// (tests/InfrastructureEfTests/Billing/EntitlementConcurrencyTests.cs), not the fast SQLite suite.
    /// RN-012: this method is intentionally NEVER called from update/delete handlers — a downgrade
    /// that puts a dealer over its new limit must not block editing or removing what it already has.
    /// </summary>
    Task<Result> EnsureCanAddAsync(Guid dealerId, PlanResource resource, CancellationToken ct = default);

    /// <summary>
    /// sfc A3.1.5: backs <c>GET billing/usage</c>. Reuses the same plan-code/limits resolution as
    /// <see cref="EnsureCanAddAsync"/>, but loads usage for every resource (the report needs all of
    /// them, unlike a single-resource check).
    /// </summary>
    Task<IReadOnlyList<PlanUsageLine>> GetUsageReportAsync(Guid dealerId, CancellationToken ct = default);
}

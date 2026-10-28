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
    /// RN-012: this method is intentionally NEVER called from a handler that only edits or removes a
    /// resource the dealer already counts — a downgrade that puts a dealer over its new limit must not
    /// block that. It IS called from a handler that makes something newly count toward a limit without
    /// inserting a new row of that resource — e.g. promoting an existing Cliente to a staff role
    /// consumes a seat exactly like creating a new staff User would, so AssignRole/UpdateUser/
    /// GrantDealerMembership call this (via <see cref="RoleConsumesASeatAsync"/>) on that specific
    /// transition, even though they are nominally "update" handlers.
    /// </summary>
    Task<Result> EnsureCanAddAsync(Guid dealerId, PlanResource resource, CancellationToken ct = default);

    /// <summary>
    /// sfc A3.1.5: backs <c>GET billing/usage</c>. Reuses the same plan-code/limits resolution as
    /// <see cref="EnsureCanAddAsync"/>, but loads usage for every resource (the report needs all of
    /// them, unlike a single-resource check).
    /// </summary>
    Task<IReadOnlyList<PlanUsageLine>> GetUsageReportAsync(Guid dealerId, CancellationToken ct = default);

    /// <summary>
    /// Security review (sfc A3.1): whether <paramref name="roleId"/> grants at least one permission —
    /// i.e. whether assigning it makes its holder count toward <see cref="PlanResource.Users"/>. A role
    /// with zero permissions (the seeded <c>Cliente</c> default, or any custom role nobody granted
    /// anything to) never consumes a seat, regardless of what it is named.
    /// </summary>
    Task<bool> RoleConsumesASeatAsync(Guid roleId, CancellationToken ct = default);

    /// <summary>
    /// Security review (sfc A3.1), third round: a role going from zero permissions to at least one
    /// retroactively turns every one of its EXISTING holders into a seat at once — a bulk promotion
    /// that <see cref="RoleConsumesASeatAsync"/> alone cannot catch, because no individual user's
    /// <c>RoleId</c> changes when the role's own permission set does. Call this from
    /// <c>UpdateRoleCommandHandler</c>, inside the same advisory-lock-guarded transaction as the save,
    /// BEFORE <c>Role.ReplacePermissions</c>, whenever the role currently has zero permissions and the
    /// requested set is non-empty. Never needed from role CREATION: a brand-new role structurally has
    /// zero holders yet, so it can never retroactively promote anyone.
    /// </summary>
    Task<Result> EnsureRoleCanGainHoldersAsync(Guid dealerId, Guid roleId, CancellationToken ct = default);
}

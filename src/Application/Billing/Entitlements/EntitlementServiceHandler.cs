using Application.Abstractions.Billing;
using Application.Abstractions.Data;
using Domain.Billing;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Billing.Entitlements;

/// <summary>
/// sfc A3.1 (A1, A21, RN-011). Per the orchestrator lock + recon finding #6 (see
/// <c>ProvisionDealerCommandHandler</c>) the Application layer cannot reference the concrete
/// <c>ApplicationDbContext</c> (it lives in Infrastructure) — the base <see cref="DbContext"/> is
/// injected alongside <see cref="IApplicationDbContext"/>; DI resolves both to the same scoped
/// instance, so raw-SQL/transaction calls on <see cref="DbContext"/> and LINQ queries on
/// <see cref="IApplicationDbContext"/> stay on the same connection/transaction.
/// </summary>
internal sealed class EntitlementServiceHandler(
    IApplicationDbContext context,
    DbContext dbContext,
    IPlanCatalog planCatalog) : IEntitlementService
{
    public async Task<Result> EnsureCanAddAsync(Guid dealerId, PlanResource resource, CancellationToken ct = default)
    {
        await AcquireLockAsync(dealerId, resource, ct);

        string planCode = await LoadPlanCodeAsync(dealerId, ct);
        PlanLimits? limits = await planCatalog.GetEntitlementsAsync(planCode, ct);
        PlanUsage usage = await LoadUsageForAsync(dealerId, resource, ct);

        return PlanEntitlementsEvaluator.CanCreate(limits, usage, resource);
    }

    public async Task<IReadOnlyList<PlanUsageLine>> GetUsageReportAsync(Guid dealerId, CancellationToken ct = default)
    {
        string planCode = await LoadPlanCodeAsync(dealerId, ct);
        PlanLimits? limits = await planCatalog.GetEntitlementsAsync(planCode, ct);
        PlanUsage usage = await LoadFullUsageAsync(dealerId, ct);

        return PlanEntitlementsEvaluator.Report(limits, usage);
    }

    private async Task<string> LoadPlanCodeAsync(Guid dealerId, CancellationToken ct)
    {
        DealerSubscription? subscription = await context.DealerSubscriptions
            .FirstOrDefaultAsync(s => s.DealerId == dealerId, ct);
        return subscription?.PlanCode ?? DealerSubscription.DefaultPlanCode;
    }

    /// <summary>Counts usage for ONLY the resource being checked — never all four on every call.</summary>
    private async Task<PlanUsage> LoadUsageForAsync(Guid dealerId, PlanResource resource, CancellationToken ct)
    {
        switch (resource)
        {
            case PlanResource.Vehicles:
                return new PlanUsage(await context.Cars.CountAsync(c => c.DealerId == dealerId, ct), 0, 0, 0);
            case PlanResource.Users:
                return new PlanUsage(0, await CountStaffUsersAsync(dealerId, ct), 0, 0);
            case PlanResource.Webhooks:
                return new PlanUsage(0, 0, 0, await context.WebhookSubscriptions.CountAsync(w => w.DealerId == dealerId, ct));
            case PlanResource.ApiKeys:
            default:
                // sfc A3.1: there is no ApiKey entity yet. Counting 0 (instead of throwing) keeps the
                // evaluator's fail-open path correct if this is ever reached — no caller passes
                // PlanResource.ApiKeys today.
                return new PlanUsage(0, 0, 0, 0);
        }
    }

    /// <summary>The usage report genuinely needs every resource at once, unlike a single-resource check.</summary>
    private async Task<PlanUsage> LoadFullUsageAsync(Guid dealerId, CancellationToken ct)
    {
        int vehicles = await context.Cars.CountAsync(c => c.DealerId == dealerId, ct);
        int users = await CountStaffUsersAsync(dealerId, ct);
        int webhooks = await context.WebhookSubscriptions.CountAsync(w => w.DealerId == dealerId, ct);
        return new PlanUsage(vehicles, users, 0, webhooks);
    }

    /// <summary>
    /// Security review (sfc A3.1): the plan's "users" limit is a staff-seat limit, not a row count.
    /// Counting every <see cref="Domain.Users.User"/> row — including self-registered customers — would
    /// let an unauthenticated visitor consume the dealer's own staff quota just by signing up.
    /// </summary>
    /// <remarks>
    /// Security review follow-up: an earlier version excluded users by <c>Role.Name == "Cliente"</c>.
    /// That name is editable by the dealer (<c>Role.Rename</c>, CFG-03) — a dealer at the limit could
    /// rename a permission-bearing role to literally "Cliente" to make its holders stop counting, or
    /// rename the real Cliente role away so ordinary customers start counting (a self-inflicted outage,
    /// but still the wrong classification). Classify by actual capability instead: a role grants at
    /// least one permission, or it does not — <c>Cliente</c> is seeded with zero permissions
    /// (<c>DefaultRoleCatalog.ClientePermissionsList</c>) and stays correctly excluded under renaming,
    /// because renaming alone never changes what a role can actually do.
    /// </remarks>
    private async Task<int> CountStaffUsersAsync(Guid dealerId, CancellationToken ct)
    {
        int homeSeats = await context.Users
            .Where(u => u.DealerId == dealerId)
            .Join(context.RolePermissions, u => u.RoleId, rp => rp.RoleId, (u, rp) => u.Id)
            .Distinct()
            .CountAsync(ct);

        // sfc B6.3: a multi-dealer membership (UserDealerMembership) is a seat in THIS dealer too,
        // even though the member's home User.DealerId points elsewhere — otherwise
        // GrantUserDealerMembershipCommandHandler's entitlement gate would check against a count
        // that this exact operation can never move, making the gate a no-op.
        int membershipSeats = await context.UserDealerMemberships
            .IgnoreQueryFilters()
            .Where(m => m.DealerId == dealerId)
            .Join(context.RolePermissions, m => m.RoleId, rp => rp.RoleId, (m, rp) => m.UserId)
            .Distinct()
            .CountAsync(ct);

        return homeSeats + membershipSeats;
    }

    /// <summary>
    /// Security review (sfc A3.1): whether assigning <paramref name="roleId"/> would make its holder
    /// count toward the plan's "users" seat limit — i.e. whether the role grants at least one
    /// permission. Exposed so role-change handlers (AssignRole, UpdateUser, GrantDealerMembership) can
    /// gate the transition that actually creates a new seat holder, which <see cref="EnsureCanAddAsync"/>
    /// alone cannot catch: no <see cref="Domain.Users.User"/> row is inserted when an existing Cliente is
    /// promoted to a staff role, so the create-time check never fires for that path.
    /// </summary>
    public Task<bool> RoleConsumesASeatAsync(Guid roleId, CancellationToken ct = default) =>
        context.RolePermissions.AnyAsync(rp => rp.RoleId == roleId, ct);

    /// <summary>
    /// Security review (sfc A3.1), third round: <see cref="RoleConsumesASeatAsync"/> alone still misses
    /// a bulk route — a dealer can put several Cliente users into a brand-new, permission-less custom
    /// role (never gated, since that role doesn't consume a seat YET), then call
    /// <c>UpdateRoleCommandHandler</c> to grant that role its first permission. Every existing holder
    /// becomes a seat in one step, with no per-user AssignRole/UpdateUser call for any of them to gate.
    /// Call this BEFORE <c>Role.ReplacePermissions</c> whenever a role is about to go from zero
    /// permissions to at least one, inside the same advisory-lock-guarded transaction as the save.
    /// </summary>
    public async Task<Result> EnsureRoleCanGainHoldersAsync(Guid dealerId, Guid roleId, CancellationToken ct = default)
    {
        await AcquireLockAsync(dealerId, PlanResource.Users, ct);

        string planCode = await LoadPlanCodeAsync(dealerId, ct);
        PlanLimits? limits = await planCatalog.GetEntitlementsAsync(planCode, ct);
        int? limit = limits?.MaxUsers;
        if (limit is null)
        {
            return Result.Success();
        }

        // The role still has zero permissions at this point (the caller checks that precondition
        // before calling), so it contributes nothing to this count yet — every one of its holders is
        // about to become NEW seat usage, on top of whatever every other role already accounts for.
        int seatsFromOtherRoles = await CountStaffUsersAsync(dealerId, ct);
        int aboutToBecomeSeats = await CountHoldersOfRoleAsync(dealerId, roleId, ct);
        int projectedTotal = seatsFromOtherRoles + aboutToBecomeSeats;

        return projectedTotal <= limit.Value
            ? Result.Success()
            : Result.Failure(SharedKernel.Error.Forbidden(
                "PLAN_LIMIT_EXCEEDED",
                $"Este cambio de rol haría que {aboutToBecomeSeats} usuario(s) pasen a ocupar un cupo del plan, superando el límite ({projectedTotal}/{limit.Value})."));
    }

    private async Task<int> CountHoldersOfRoleAsync(Guid dealerId, Guid roleId, CancellationToken ct)
    {
        int homeHolders = await context.Users.CountAsync(u => u.DealerId == dealerId && u.RoleId == roleId, ct);
        int membershipHolders = await context.UserDealerMemberships
            .IgnoreQueryFilters()
            .CountAsync(m => m.DealerId == dealerId && m.RoleId == roleId, ct);
        return homeHolders + membershipHolders;
    }

    public Task AcquireUsersLockAsync(Guid dealerId, CancellationToken ct = default) =>
        AcquireLockAsync(dealerId, PlanResource.Users, ct);

    private async Task AcquireLockAsync(Guid dealerId, PlanResource resource, CancellationToken ct)
    {
        if (!dbContext.Database.IsRelational())
        {
            return;
        }

        // Two deterministic int keys (dealer, resource) — avoids any hashing-collision concern from
        // packing both into a single bigint key. Postgres-only: pg_advisory_xact_lock has no
        // SQLite/InMemory equivalent, which is exactly why this is guarded by IsRelational().
        // Reentrant within one session/transaction: calling this twice on the same key (e.g. once
        // from AssignRoleCommandHandler via AcquireUsersLockAsync, then again from the
        // EnsureCanAddAsync it calls afterward) is safe, not a self-deadlock.
        int dealerLockKey = dealerId.GetHashCode();
        int resourceLockKey = (int)resource;
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock({dealerLockKey}, {resourceLockKey})", ct);
    }
}

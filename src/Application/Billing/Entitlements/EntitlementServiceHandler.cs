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
        if (dbContext.Database.IsRelational())
        {
            // Two deterministic int keys (dealer, resource) — avoids any hashing-collision concern
            // from packing both into a single bigint key. Postgres-only: pg_advisory_xact_lock has no
            // SQLite/InMemory equivalent, which is exactly why this branch is guarded by IsRelational().
            int dealerLockKey = dealerId.GetHashCode();
            int resourceLockKey = (int)resource;
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock({dealerLockKey}, {resourceLockKey})", ct);
        }

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
    /// Counting every <see cref="Domain.Users.User"/> row — including self-registered customers, who
    /// hold the dealer-scoped <c>Cliente</c> role and never go through any seat-granting flow — would
    /// let an unauthenticated visitor consume the dealer's own staff quota just by signing up. Exclude
    /// that role explicitly rather than trying to enumerate every staff role name.
    /// </summary>
    private async Task<int> CountStaffUsersAsync(Guid dealerId, CancellationToken ct)
    {
        Guid? clienteRoleId = await context.Roles
            .Where(r => r.DealerId == dealerId && r.Name == Domain.Users.DefaultRoleCatalog.Cliente)
            .Select(r => (Guid?)r.Id)
            .FirstOrDefaultAsync(ct);

        return await context.Users
            .CountAsync(u => u.DealerId == dealerId && u.RoleId != clienteRoleId, ct);
    }
}

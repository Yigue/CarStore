using Application.Abstractions.Authentication;
using Application.Abstractions.Billing;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Billing;
using Domain.DealerSettings;
using Domain.DealerSettings.Events;
using Domain.Users;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using SharedKernel;
using DealerSettingsEntity = Domain.DealerSettings.DealerSettings;

namespace Application.Dealers.Provision;

/// <summary>
/// Atomically provisions a new dealer tenant (DealerSettings row) and its first
/// Admin user inside a single EF Core transaction. On any failure both writes
/// roll back — no orphaned <see cref="DealerSettingsEntity"/> rows can exist without
/// an admin. After commit, raises <see cref="DealerProvisionedDomainEvent"/> so
/// the welcome email handler can deliver the dealer provisioning message.
///
/// Per design ADR-1 the row PK (<see cref="Entity.Id"/>) and the tenant FK
/// (<see cref="Entity.DealerId"/>) share the same freshly-minted
/// <see cref="Guid"/> — the existing <c>DealerSettings</c> ctor already enforces
/// this pattern; we just pass the same value for both.
///
/// Per the orchestrator lock + recon finding #6 the Application layer cannot
/// reference the concrete <c>ApplicationDbContext</c> (it lives in
/// Infrastructure). We inject the base <see cref="DbContext"/> type instead —
/// DI resolves both <see cref="IApplicationDbContext"/> and
/// <see cref="DbContext"/> to the same scoped <c>ApplicationDbContext</c>
/// instance, so this preserves identity while staying inside the layering
/// rules.
/// </summary>
internal sealed class ProvisionDealerCommandHandler(
    IApplicationDbContext context,
    DbContext dbContext,
    IPasswordHasher passwordHasher,
    IPublisher publisher,
    ISubscriptionGateway subscriptionGateway,
    ILogger<ProvisionDealerCommandHandler> logger,
    Application.Legal.LegalAcceptanceRecorder legalRecorder)
    : ICommandHandler<ProvisionDealerCommand, ProvisionDealerResponse>
{
    private const string DashboardBaseUrl = "carstore.com";

    public async Task<Result<ProvisionDealerResponse>> Handle(
        ProvisionDealerCommand command,
        CancellationToken cancellationToken)
    {
        // RN-016: the admin must have accepted the currently published T&C version.
        Result termsCheck = legalRecorder.EnsureCurrentVersion(command.AcceptedTermsVersion);
        if (termsCheck.IsFailure)
        {
            return Result.Failure<ProvisionDealerResponse>(termsCheck.Error);
        }

        // REQ: subdomain must be stored lowercase (TenantResolutionMiddleware
        // matches lowercase against the Host header).
        var subdomain = command.Subdomain.Trim().ToLowerInvariant();
        var dealerId = Guid.NewGuid();

        string? stripeCustomerId = null;
        try
        {
            stripeCustomerId = await subscriptionGateway.CreateCustomerAsync(dealerId, command.AdminEmail, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create Stripe customer for dealer {DealerId}", dealerId);
        }

        // In-memory DB providers (used by unit tests) don't support transactions.
        // Relational providers do, and EF SaveChanges is already per-call atomic —
        // the transaction adds cross-row atomicity for real DBs.
        IDbContextTransaction? transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;

        User user;

        try
        {
            var settings = new DealerSettingsEntity(
                id: dealerId,
                dealerId: dealerId,
                command.DealerName,
                command.AdminEmail,
                notificationsEnabled: true,
                hostName: subdomain);

            if (stripeCustomerId is not null)
            {
                settings.SetStripeCustomerId(stripeCustomerId);
            }

            context.DealerSettings.Add(settings);

            var adminRole = new Role(dealerId, "Admin", "Administrador del Sistema");
            var defaultPermissions = new[]
            {
                "cars:read", "cars:create", "cars:update", "cars:delete",
                "clients:read", "clients:write", "clients:delete",
                "sales:read", "sales:create", "sales:update", "sales:delete",
                "quotes:read", "quotes:create", "quotes:update", "quotes:delete", "quotes:accept", "quotes:reject",
                "financial:read", "financial:create", "financial:update", "financial:delete",
                "users:read", "users:create", "users:access", "CanManageUsers", "CanManageRoles",
                "CanManageSettings",
                "leads:read", "leads:create", "leads:update", "leads:delete",
                "leads:write", "leads:archive",
                "appointments:read", "appointments:create", "appointments:update", "appointments:delete",
                "admin:backfill",
                "webhooks:manage",
                // qa-p1-integridad PR6 (D7): grant at both seed sites — see UsersSeeder.cs.
                "documents:read", "documents:create"
            };

            foreach (var permission in defaultPermissions)
            {
                adminRole.AddPermission(permission);
            }

            context.Roles.Add(adminRole);

            var clienteRole = new Role(dealerId, "Cliente", "Cliente registrado");
            context.Roles.Add(clienteRole);

            user = new User(
                dealerId,
                command.AdminEmail,
                command.AdminFirstName,
                command.AdminLastName,
                passwordHasher.Hash(command.AdminPassword),
                adminRole.Id);

            context.Users.Add(user);

            legalRecorder.RecordTermsAcceptance(dealerId, user.Id, command.AcceptanceIp, command.AcceptanceUserAgent);

            // spr ADR B8: a dealer NEVER exists without a subscription row. It is created in the
            // same transaction as the dealer, with or without a Stripe customer, as Incomplete:
            // SubscriptionGuardMiddleware blocks it (402) until Stripe confirms a trial/payment, so
            // the product is not usable for free while checkout is pending.
            context.DealerSubscriptions.Add(
                DealerSubscription.CreatePending(dealerId, stripeCustomerId, planId: string.Empty));

            await dbContext.SaveChangesAsync(cancellationToken);

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
        }
        catch (DbUpdateException ex) when (IsHostNameUniqueViolation(ex))
        {
            // Lost the race against a concurrent provisioning for the same slug.
            // The DB unique index is the source of truth — translate to a
            // Conflict so the FE can surface a clear field-level error.
            if (transaction is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }
            return Result.Failure<ProvisionDealerResponse>(DealerSettingsErrors.HostNameNotUnique);
        }
        catch
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }
            throw;
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }

        var dashboardUrl = $"https://{subdomain}.{DashboardBaseUrl}/dashboard";

        await publisher.Publish(
            new DealerProvisionedDomainEvent(dealerId, user.Id, command.AdminEmail, subdomain, dashboardUrl),
            cancellationToken);

        string? checkoutUrl = null;
        try
        {
            checkoutUrl = await subscriptionGateway.CreateCheckoutSessionAsync(dealerId, command.AdminEmail, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create Stripe checkout session for dealer {DealerId}", dealerId);
        }

        return Result.Success(new ProvisionDealerResponse(dealerId, user.Id, subdomain, checkoutUrl));
    }

    /// <summary>
    /// Detects a unique-index violation on <c>DealerSettings.HostName</c>.
    /// Provider-agnostic — checks the inner exception message rather than a
    /// provider-specific error code so it works against both Npgsql and the
    /// SQLite test in-memory store.
    /// </summary>
    private static bool IsHostNameUniqueViolation(DbUpdateException ex)
    {
        var message = ex.InnerException?.Message ?? string.Empty;
        return message.Contains("HostName", StringComparison.OrdinalIgnoreCase)
            || message.Contains("host_name", StringComparison.OrdinalIgnoreCase)
            || message.Contains("dealer_settings", StringComparison.OrdinalIgnoreCase) && message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase);
    }
}
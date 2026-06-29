using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.DealerSettings.Events;
using Domain.Users;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
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
    IPublisher publisher)
    : ICommandHandler<ProvisionDealerCommand, ProvisionDealerResponse>
{
    private const string DashboardBaseUrl = "carstore.com";

    public async Task<Result<ProvisionDealerResponse>> Handle(
        ProvisionDealerCommand command,
        CancellationToken cancellationToken)
    {
        // REQ: subdomain must be stored lowercase (TenantResolutionMiddleware
        // matches lowercase against the Host header).
        var subdomain = command.Subdomain.Trim().ToLowerInvariant();

        // In-memory DB providers (used by unit tests) don't support transactions.
        // Relational providers do, and EF SaveChanges is already per-call atomic —
        // the transaction adds cross-row atomicity for real DBs.
        IDbContextTransaction? transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;

        try
        {
            var dealerId = Guid.NewGuid();

            var settings = new DealerSettingsEntity(
                id: dealerId,
                dealerId: dealerId,
                command.DealerName,
                command.AdminEmail,
                notificationsEnabled: true,
                hostName: subdomain);

            context.DealerSettings.Add(settings);

            var user = new User(
                dealerId,
                command.AdminEmail,
                command.AdminFirstName,
                command.AdminLastName,
                passwordHasher.Hash(command.AdminPassword),
                UserRole.Admin);

            context.Users.Add(user);

            await dbContext.SaveChangesAsync(cancellationToken);

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            var dashboardUrl = $"https://{subdomain}.{DashboardBaseUrl}/dashboard";

            await publisher.Publish(
                new DealerProvisionedDomainEvent(dealerId, user.Id, subdomain, dashboardUrl),
                cancellationToken);

            return Result.Success(new ProvisionDealerResponse(dealerId, user.Id, subdomain));
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
    }
}
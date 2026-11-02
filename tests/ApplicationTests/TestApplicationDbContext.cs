using Application.Abstractions.Data;
using Domain.Appointments;
using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Clients;
using Domain.Financial;
using Domain.Financial.Attributes;
using Domain.Quotes;
using Domain.Sales;
using Domain.Users;
using Domain.Shared;
using Domain.Leads;
using Domain.Billing;
using Domain.Webhooks;
using Microsoft.EntityFrameworkCore;
using DealerSettingsEntity = Domain.DealerSettings.DealerSettings;

namespace Application.UnitTests;

internal sealed class TestApplicationDbContext : DbContext, IApplicationDbContext
{
    private readonly Guid _dealerId;

    public Guid TenantDealerId => _dealerId;

    public TestApplicationDbContext(DbContextOptions<TestApplicationDbContext> options) : base(options)
    {
        _dealerId = Guid.NewGuid();
    }

    public TestApplicationDbContext(DbContextOptions<TestApplicationDbContext> options, Guid dealerId) : base(options)
    {
        _dealerId = dealerId;
    }

    public DbSet<Car> Cars => Set<Car>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Quote> Quotes => Set<Quote>();
    public DbSet<Marca> Marca => Set<Marca>();
    public DbSet<Modelo> Modelo => Set<Modelo>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<FinancialTransaction> Transactions => Set<FinancialTransaction>();
    public DbSet<TransactionCategory> TransactionCategories => Set<TransactionCategory>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserPermission> UserPermissions => Set<UserPermission>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<UserActionToken> UserActionTokens => Set<UserActionToken>();

    /// <summary>InMemory has no ExecuteUpdate: mirror the atomic rule through the domain method.</summary>
    public async Task<bool> TryConsumeUserActionTokenAsync(Guid tokenId, DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        UserActionToken? token = await UserActionTokens.FirstOrDefaultAsync(t => t.Id == tokenId, cancellationToken);
        if (token is null || !token.TryConsume(nowUtc))
        {
            return false;
        }

        await SaveChangesAsync(cancellationToken);
        return true;
    }
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<CarImage> CarImages => Set<CarImage>();
    public DbSet<ReconditioningTask> ReconditioningTasks => Set<ReconditioningTask>();
    public DbSet<DealerSettingsEntity> DealerSettings => Set<DealerSettingsEntity>();
    public DbSet<Lead> Leads => Set<Lead>();
    public DbSet<LeadActivity> LeadActivities => Set<LeadActivity>();
    public DbSet<Domain.Documents.Document> Documents => Set<Domain.Documents.Document>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<BackfillAudit> BackfillAudits => Set<BackfillAudit>();
    public DbSet<DealerSubscription> DealerSubscriptions => Set<DealerSubscription>();
    public DbSet<Domain.Legal.LegalAcceptance> LegalAcceptances => Set<Domain.Legal.LegalAcceptance>();
    /// <summary>One-shot hook fired the first time <see cref="TenantExports"/> is touched (race simulation).</summary>
    public Action? OnFirstTenantExportsAccess { get; set; }

    public DbSet<Domain.Tenancy.TenantExport> TenantExports
    {
        get
        {
            Action? hook = OnFirstTenantExportsAccess;
            OnFirstTenantExportsAccess = null;
            hook?.Invoke();
            return Set<Domain.Tenancy.TenantExport>();
        }
    }
    public DbSet<Domain.Tenancy.TenantPurgeRecord> TenantPurgeRecords => Set<Domain.Tenancy.TenantPurgeRecord>();
    public DbSet<Domain.Tenancy.PendingStorageDeletion> PendingStorageDeletions => Set<Domain.Tenancy.PendingStorageDeletion>();
    public DbSet<ProcessedStripeEvent> ProcessedStripeEvents => Set<ProcessedStripeEvent>();
    public DbSet<BillingReconciliationFlag> BillingReconciliationFlags => Set<BillingReconciliationFlag>();
    public DbSet<BillingRefundRecord> BillingRefundRecords => Set<BillingRefundRecord>();
    public DbSet<WebhookSubscription> WebhookSubscriptions => Set<WebhookSubscription>();
    public DbSet<WebhookDelivery> WebhookDeliveries => Set<WebhookDelivery>();
    public DbSet<Domain.Platform.PlatformAuditLogEntry> PlatformAuditLogs => Set<Domain.Platform.PlatformAuditLogEntry>();
    public DbSet<Domain.Auditing.DealerAuditLogEntry> DealerAuditLogs => Set<Domain.Auditing.DealerAuditLogEntry>();
    public DbSet<Domain.Currency.DealerExchangeRate> DealerExchangeRates => Set<Domain.Currency.DealerExchangeRate>();
    public DbSet<Domain.Deposits.Deposit> Deposits => Set<Domain.Deposits.Deposit>();
    public DbSet<Domain.Users.UserPreferences> UserPreferences => Set<Domain.Users.UserPreferences>();
    public DbSet<Domain.Cars.VehicleLien> VehicleLiens => Set<Domain.Cars.VehicleLien>();
    public DbSet<Domain.Suppliers.Supplier> Suppliers => Set<Domain.Suppliers.Supplier>();
    public DbSet<Domain.AfterSales.WarrantyRecord> WarrantyRecords => Set<Domain.AfterSales.WarrantyRecord>();
    public DbSet<Domain.AfterSales.ServiceReminder> ServiceReminders => Set<Domain.AfterSales.ServiceReminder>();
    public DbSet<Domain.Branches.Branch> Branches => Set<Domain.Branches.Branch>();
    public DbSet<Domain.Branches.UserBranch> UserBranches => Set<Domain.Branches.UserBranch>();
    public DbSet<Domain.Marketing.Subscriber> Subscribers => Set<Domain.Marketing.Subscriber>();
    public DbSet<Domain.Integrations.DealerIntegration> DealerIntegrations => Set<Domain.Integrations.DealerIntegration>();
    public DbSet<Domain.Integrations.IntegrationCommand> IntegrationCommands => Set<Domain.Integrations.IntegrationCommand>();
    public DbSet<Domain.Integrations.ExternalListing> ExternalListings => Set<Domain.Integrations.ExternalListing>();
    public DbSet<Domain.Leads.ProcessedMetaLeadEvent> ProcessedMetaLeadEvents => Set<Domain.Leads.ProcessedMetaLeadEvent>();
    public DbSet<Domain.Commissions.SaleCommission> SaleCommissions => Set<Domain.Commissions.SaleCommission>();
    public DbSet<Domain.Commissions.CommissionPolicy> CommissionPolicies => Set<Domain.Commissions.CommissionPolicy>();
    public DbSet<Domain.Users.UserDealerMembership> UserDealerMemberships => Set<Domain.Users.UserDealerMembership>();
    public DbSet<Domain.Common.DealerSequence> DealerSequences => Set<Domain.Common.DealerSequence>();

    public void DetachEntity(object entity)
    {
        Entry(entity).State = EntityState.Detached;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Apply configurations from the Infrastructure assembly
        // This ensures all entity configurations (including value object conversions) are used
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(Infrastructure.Database.ApplicationDbContext).Assembly);

        // RowVersion is mapped to the Postgres xmin system column (see DealerSettingsConfiguration).
        // For InMemory provider, the xmin shadow property exists (UseXminAsConcurrencyToken)
        // but the explicit RowVersion property must be ignored to avoid "no column" errors.
        // RowVersion will default to 0 for all InMemory entities (accepted in tests).
        modelBuilder.Entity<DealerSettingsEntity>().Ignore(s => s.RowVersion);

        base.OnModelCreating(modelBuilder);
    }
}

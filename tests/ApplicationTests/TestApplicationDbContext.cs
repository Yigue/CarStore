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
    public DbSet<ProcessedStripeEvent> ProcessedStripeEvents => Set<ProcessedStripeEvent>();
    public DbSet<WebhookSubscription> WebhookSubscriptions => Set<WebhookSubscription>();
    public DbSet<WebhookDelivery> WebhookDeliveries => Set<WebhookDelivery>();
    public DbSet<Domain.Platform.PlatformAuditLogEntry> PlatformAuditLogs => Set<Domain.Platform.PlatformAuditLogEntry>();

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

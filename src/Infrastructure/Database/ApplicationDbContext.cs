using Application.Abstractions.Data;
using Application.Abstractions.Tenancy;
using Domain.Appointments;
using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Clients;
using Domain.Financial;
using Domain.Financial.Attributes;
using Domain.Leads;
using Domain.Quotes;
using Domain.Sales;

using Domain.Users;
using DealerSettingsEntity = Domain.DealerSettings.DealerSettings;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using SharedKernel;
using Newtonsoft.Json;
using Domain.Shared;
using Infrastructure.Persistence.Configurations.ValueObjects;
using Domain.Billing;
using Domain.Webhooks;

namespace Infrastructure.Database;

public sealed class ApplicationDbContext : DbContext, IApplicationDbContext
{
    private readonly IPublisher publisher;
    private readonly ICurrentTenantService _tenantService;

    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options, 
        IPublisher _publisher,
        ICurrentTenantService tenantService) : base(options)
    {
        publisher = _publisher;
        _tenantService = tenantService;
    }

    public DbSet<Car> Cars { get; set; }
    public DbSet<Client> Clients { get; set; }
    public DbSet<Quote> Quotes { get; set; }
    public DbSet<Sale> Sales { get; set; }
    public DbSet<Modelo> Modelo { get; set; }
    public DbSet<Marca> Marca { get; set; }
    public DbSet<FinancialTransaction> Transactions { get; set; }
    public DbSet<TransactionCategory> TransactionCategories { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<UserPermission> UserPermissions { get; set; }
    public DbSet<Role> Roles { get; set; }
    public DbSet<RolePermission> RolePermissions { get; set; }
    public DbSet<PasswordResetToken> PasswordResetTokens { get; set; }
    public DbSet<OutboxMessage> OutboxMessages { get; set; }
    public DbSet<CarImage> CarImages { get; set; }
    public DbSet<ReconditioningTask> ReconditioningTasks { get; set; }
    public DbSet<DealerSettingsEntity> DealerSettings { get; set; }
    public DbSet<Lead> Leads { get; set; }
    public DbSet<Domain.Documents.Document> Documents { get; set; }
    public DbSet<Appointment> Appointments { get; set; }
    public DbSet<BackfillAudit> BackfillAudits { get; set; }
    public DbSet<DealerSubscription> DealerSubscriptions { get; set; }
    public DbSet<ProcessedStripeEvent> ProcessedStripeEvents { get; set; }
    public DbSet<WebhookSubscription> WebhookSubscriptions { get; set; }
    public DbSet<WebhookDelivery> WebhookDeliveries { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        if (Database.ProviderName != "Microsoft.EntityFrameworkCore.Sqlite")
        {
            modelBuilder.HasDefaultSchema(Schemas.Default);

            // REQ-VMS-7: partial UNIQUE index "one cover per car" (Postgres only — the filter
            // SQL is not portable to SQLite's EnsureCreated()).
            modelBuilder.Entity<CarImage>()
                .HasIndex(ci => ci.CarId)
                .IsUnique()
                .HasFilter("is_cover = true")
                .HasDatabaseName("ux_car_images_car_id_is_cover");

            // Map RowVersion to the Postgres xmin system column (concurrency token).
            // xmin is a Postgres system column — no CREATE COLUMN in migrations.
            // The manual migration 20260628_AddDealerSuspensionColumns does NOT include xmin;
            // this mapping is Postgres-runtime-only.
            modelBuilder.Entity<DealerSettingsEntity>()
                .Property(s => s.RowVersion)
                .HasColumnName("xmin")
                .HasColumnType("xid")
                .IsConcurrencyToken()
                .ValueGeneratedOnAddOrUpdate();
        }

        var isActiveFilter = Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite" 
            ? "\"IsActive\" = 1" 
            : "is_active = true";

        modelBuilder.Entity<DealerSettingsEntity>()
            .HasIndex(s => new { s.HostName, s.IsActive })
            .HasDatabaseName("ix_dealer_settings_host_name_active_lookup")
            .IsUnique(false)
            .HasFilter(isActiveFilter);

        // Ignorar DealerId en entidades compartidas (catálogo)
        modelBuilder.Entity<Marca>().Ignore(x => x.DealerId);
        modelBuilder.Entity<Modelo>().Ignore(x => x.DealerId);
        modelBuilder.Entity<TransactionCategory>().Ignore(x => x.DealerId);
        modelBuilder.Entity<CarImage>().Ignore(x => x.DealerId);

        // User configuration (Email + Role + length constraints) vive en
        // Infrastructure/Database/Configurations/UserConfiguration.cs y se
        // aplica via ApplyConfigurationsFromAssembly() arriba.

        // Multi-tenancy: Global Query Filters
        // Automatically filter all queries by DealerId
        // This ensures data isolation between tenants (concesionarias)
        // 
        // NOTE: We use the _tenantService field in the expression so EF Core 
        // evaluates DealerId at query time, not at model building time.
        // When HasTenant is false (migrations/background jobs), filters are not applied.
        
        modelBuilder.Entity<Car>().HasQueryFilter(x => 
            !_tenantService.HasTenant || x.DealerId == _tenantService.DealerId);
        modelBuilder.Entity<Client>().HasQueryFilter(x =>
            (!_tenantService.HasTenant || x.DealerId == _tenantService.DealerId) && !x.IsDeleted);
        modelBuilder.Entity<Quote>().HasQueryFilter(x =>
            (!_tenantService.HasTenant || x.DealerId == _tenantService.DealerId) && !x.IsDeleted);
        modelBuilder.Entity<Sale>().HasQueryFilter(x => 
            !_tenantService.HasTenant || x.DealerId == _tenantService.DealerId);
        modelBuilder.Entity<FinancialTransaction>().HasQueryFilter(x => 
            !_tenantService.HasTenant || x.DealerId == _tenantService.DealerId);
        modelBuilder.Entity<User>().HasQueryFilter(x =>
            !_tenantService.HasTenant || x.DealerId == _tenantService.DealerId);
        modelBuilder.Entity<DealerSettingsEntity>().HasQueryFilter(x =>
            !_tenantService.HasTenant || x.DealerId == _tenantService.DealerId);
        modelBuilder.Entity<Lead>().HasQueryFilter(x =>
            !_tenantService.HasTenant || x.DealerId == _tenantService.DealerId);
        modelBuilder.Entity<Domain.Documents.Document>().HasQueryFilter(x =>
            !_tenantService.HasTenant || x.DealerId == _tenantService.DealerId);
        modelBuilder.Entity<ReconditioningTask>().HasQueryFilter(x =>
            !_tenantService.HasTenant || x.DealerId == _tenantService.DealerId);
        modelBuilder.Entity<Appointment>().HasQueryFilter(x =>
            !_tenantService.HasTenant || x.DealerId == _tenantService.DealerId);
        modelBuilder.Entity<BackfillAudit>().HasQueryFilter(x =>
            !_tenantService.HasTenant || x.DealerId == _tenantService.DealerId);
        modelBuilder.Entity<DealerSubscription>().HasQueryFilter(x =>
            !_tenantService.HasTenant || x.DealerId == _tenantService.DealerId);
        modelBuilder.Entity<WebhookSubscription>().HasQueryFilter(x =>
            !_tenantService.HasTenant || x.DealerId == _tenantService.DealerId);
        modelBuilder.Entity<WebhookDelivery>().HasQueryFilter(x =>
            !_tenantService.HasTenant || x.DealerId == _tenantService.DealerId);
        // Note: Marca, Modelo, TransactionCategory, CarImage are shared across tenants (catalog data)
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.ConfigureWarnings(warnings =>
            warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.CoreEventId.ManyServiceProvidersCreatedWarning)
                    .Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
    }

    // Implement interface methods
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var outboxMessages = ChangeTracker
            .Entries<Entity>()
            .Select(entry => entry.Entity)
            .SelectMany(entity =>
            {
                var domainEvents = entity.DomainEvents;
                entity.ClearDomainEvents();
                return domainEvents.Select(ev => (entity, ev));
            })
            .Select(pair => new OutboxMessage
            {
                Id = Guid.NewGuid(),
                OccurredOnUtc = DateTime.UtcNow,
                Type = pair.ev.GetType().Name,
                Content = JsonConvert.SerializeObject(
                    pair.ev,
                    new JsonSerializerSettings
                    {
                        TypeNameHandling = TypeNameHandling.None
                    }),
                AggregateId = pair.entity.Id,
                AggregateType = pair.entity.GetType().Name,
                DealerId = pair.entity.DealerId == Guid.Empty ? null : pair.entity.DealerId
            })
            .ToList();

        AddRange(outboxMessages);

        return await base.SaveChangesAsync(cancellationToken);
    }
}

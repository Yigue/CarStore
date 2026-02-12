using Application.Abstractions.Data;
using Application.Abstractions.Tenancy;
using Domain.Cars;
using Domain.Cars.Atribbutes;
using Domain.Clients;
using Domain.Financial;
using Domain.Financial.Attributes;
using Domain.Quotes;
using Domain.Sales;

using Domain.Users;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using SharedKernel;
using Newtonsoft.Json;
using Domain.Shared;

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
    public DbSet<OutboxMessage> OutboxMessages { get; set; }
    public DbSet<CarImage> CarImages { get; set; }


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        modelBuilder.HasDefaultSchema(Schemas.Default);
        
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
            !_tenantService.HasTenant || x.DealerId == _tenantService.DealerId);
        modelBuilder.Entity<Quote>().HasQueryFilter(x => 
            !_tenantService.HasTenant || x.DealerId == _tenantService.DealerId);
        modelBuilder.Entity<Sale>().HasQueryFilter(x => 
            !_tenantService.HasTenant || x.DealerId == _tenantService.DealerId);
        modelBuilder.Entity<FinancialTransaction>().HasQueryFilter(x => 
            !_tenantService.HasTenant || x.DealerId == _tenantService.DealerId);
        modelBuilder.Entity<User>().HasQueryFilter(x => 
            !_tenantService.HasTenant || x.DealerId == _tenantService.DealerId);
        // Note: Marca, Modelo, TransactionCategory, CarImage are shared across tenants (catalog data)
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.ConfigureWarnings(warnings =>
            warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.CoreEventId.ManyServiceProvidersCreatedWarning));
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
                return domainEvents;
            })
            .Select(domainEvent => new OutboxMessage
            {
                Id = Guid.NewGuid(),
                OccurredOnUtc = DateTime.UtcNow,
                Type = domainEvent.GetType().Name,
                Content = JsonConvert.SerializeObject(
                    domainEvent,
                    new JsonSerializerSettings
                    {
                        TypeNameHandling = TypeNameHandling.All
                    })
            })
            .ToList();

        AddRange(outboxMessages);

        return await base.SaveChangesAsync(cancellationToken);
    }
}

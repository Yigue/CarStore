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
    public DbSet<UserPermission> UserPermissions => Set<UserPermission>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<CarImage> CarImages => Set<CarImage>();
    public DbSet<ReconditioningTask> ReconditioningTasks => Set<ReconditioningTask>();
    public DbSet<DealerSettingsEntity> DealerSettings => Set<DealerSettingsEntity>();
    public DbSet<Lead> Leads => Set<Lead>();
    public DbSet<Domain.Documents.Document> Documents => Set<Domain.Documents.Document>();
    public DbSet<Appointment> Appointments => Set<Appointment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Apply configurations from the Infrastructure assembly
        // This ensures all entity configurations (including value object conversions) are used
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(Infrastructure.Database.ApplicationDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}

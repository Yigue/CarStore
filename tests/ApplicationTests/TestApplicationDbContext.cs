using Application.Abstractions.Data;
using Domain.Cars;
using Domain.Cars.Atribbutes;
using Domain.Clients;
using Domain.Financial;
using Domain.Financial.Attributes;
using Domain.Quotes;
using Domain.Sales;
using Domain.Users;
using Domain.Shared;
using Microsoft.EntityFrameworkCore;

namespace Application.UnitTests;

internal sealed class TestApplicationDbContext : DbContext, IApplicationDbContext
{
    public TestApplicationDbContext(DbContextOptions<TestApplicationDbContext> options) : base(options)
    {
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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Client>().OwnsOne(c => c.Email);
        modelBuilder.Entity<Car>().OwnsOne(c => c.Patente);
        modelBuilder.Entity<Car>().OwnsOne(c => c.Price);
        modelBuilder.Entity<Sale>().OwnsOne(s => s.FinalPrice);
        modelBuilder.Entity<Quote>().OwnsOne(q => q.ProposedPrice);
        modelBuilder.Entity<FinancialTransaction>().OwnsOne(t => t.Amount);
        
        base.OnModelCreating(modelBuilder);
    }
}

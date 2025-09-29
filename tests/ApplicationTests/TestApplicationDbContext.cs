using Application.Abstractions.Data;
using Domain.Cars;
using Domain.Cars.Atribbutes;
using Domain.Clients;
using Domain.Financial;
using Domain.Financial.Attributes;
using Domain.Quotes;
using Domain.Sales;
using Domain.Users;
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
    public DbSet<CarImage> CarImages => Set<CarImage>();
}

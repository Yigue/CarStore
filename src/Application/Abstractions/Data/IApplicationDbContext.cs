using Domain.Cars;
using Domain.Cars.Atribbutes;
using Domain.Clients;
using Domain.Financial;
using Domain.Financial.Attributes;
using Domain.Quotes;
using Domain.Sales;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

namespace Application.Abstractions.Data;

public interface IApplicationDbContext
{
    DbSet<Car> Cars { get; }
    DbSet<Client> Clients { get; }
    DbSet<Quote> Quotes { get; }
    DbSet<Marca> Marca { get; }
    DbSet<Modelo> Modelo { get; }
    DbSet<Sale> Sales { get; }
    DbSet<FinancialTransaction> Transactions { get; }
    DbSet<TransactionCategory> TransactionCategories { get; }
    DbSet<User> Users { get; }
    DbSet<CarImage> CarImages { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

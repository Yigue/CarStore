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
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

using Domain.Shared;
using DealerSettingsEntity = Domain.DealerSettings.DealerSettings;

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
    DbSet<UserPermission> UserPermissions { get; }
    DbSet<OutboxMessage> OutboxMessages { get; }
    DbSet<CarImage> CarImages { get; }
    DbSet<ReconditioningTask> ReconditioningTasks { get; }
    DbSet<DealerSettingsEntity> DealerSettings { get; }
    DbSet<Lead> Leads { get; }
    DbSet<Domain.Documents.Document> Documents { get; }
    DbSet<Appointment> Appointments { get; }
    DbSet<BackfillAudit> BackfillAudits { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

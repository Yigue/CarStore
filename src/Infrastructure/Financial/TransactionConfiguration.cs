using Domain.Financial;
using Infrastructure.Persistence.Configurations.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations.Financial;

public class TransactionConfiguration : IEntityTypeConfiguration<FinancialTransaction>
{
    public void Configure(EntityTypeBuilder<FinancialTransaction> builder)
    {
        builder.HasKey(t => t.Id);

        // REQ-FIN-INDEX-001 + REQ-FIN-LEDGER-001:
        //   - (DealerId, TransactionDate) — supports the summary endpoint's
        //     tenant-scoped WHERE + ORDER BY.
        //   - (DealerId, CategoryId) — supports the toolbar's category filter.
        //   - (ReconditioningTaskId, SourceId) — partial unique index; DB
        //     floor for ledger idempotency. The partial filter is applied in
        //     the generated migration via .HasFilter().
        builder.HasIndex(t => new { t.DealerId, t.TransactionDate })
            .HasDatabaseName("IX_transactions_DealerId_TransactionDate");

        builder.HasIndex(t => new { t.DealerId, t.CategoryId })
            .HasDatabaseName("IX_transactions_DealerId_CategoryId");

        builder.HasIndex(t => new { t.ReconditioningTaskId, t.SourceId })
            .HasDatabaseName("IX_transactions_ReconditioningTaskId_SourceId")
            .IsUnique()
            .HasFilter("\"ReconditioningTaskId\" IS NOT NULL AND \"SourceId\" IS NOT NULL");

        builder.Property(t => t.Amount)
            .HasConversion(new MoneyValueConverter())
            .HasColumnName("amount")
            .IsRequired();

        builder.Property(t => t.Type)
            .IsRequired();

        builder.Property(t => t.PaymentMethod)
            .IsRequired();

        builder.Property(t => t.Description)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(t => t.ReferenceNumber)
            .HasMaxLength(50);

        builder.Property(t => t.TransactionDate)
            .IsRequired();

        builder.HasOne(t => t.Car)
            .WithMany()
            .HasForeignKey(t => t.CarId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.Client)
            .WithMany()
            .HasForeignKey(t => t.ClientId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.Sale)
            .WithMany()
            .HasForeignKey(t => t.SaleId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.Category)
            .WithMany()
            .HasForeignKey(t => t.CategoryId)
            .IsRequired();

        // Configuración explícita de propiedades de navegación
        builder.Navigation(t => t.Category).AutoInclude();
        builder.Navigation(t => t.Car).AutoInclude();
        builder.Navigation(t => t.Client).AutoInclude();
        builder.Navigation(t => t.Sale).AutoInclude();
    }
}

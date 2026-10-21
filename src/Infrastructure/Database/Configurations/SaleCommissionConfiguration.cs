using Domain.Commissions;
using Infrastructure.Persistence.Configurations.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Database.Configurations;

/// <summary>
/// sfc B4.1 / RN-008 / RN-009: one frozen commission snapshot per sale. The unique index on
/// <c>SaleId</c> is the real idempotency guard — <c>CalculateSaleCommissionHandler</c> checks before
/// inserting, but a concurrent duplicate insert is rejected here, not silently accepted twice.
/// </summary>
internal sealed class SaleCommissionConfiguration : IEntityTypeConfiguration<SaleCommission>
{
    public void Configure(EntityTypeBuilder<SaleCommission> builder)
    {
        builder.ToTable("sale_commissions");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.DealerId).IsRequired();
        builder.Property(c => c.SaleId).IsRequired();
        builder.Property(c => c.SalespersonId);
        builder.OwnsRequiredMoney(c => c.Base, "base_amount", "base_currency");
        builder.Property(c => c.Rate).HasPrecision(5, 2);
        builder.OwnsRequiredMoney(c => c.Amount, "amount", "amount_currency");
        builder.Property(c => c.IsConfigured).IsRequired();
        builder.Property(c => c.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(c => c.CalculatedAtUtc).IsRequired();

        builder.Ignore(c => c.DomainEvents);

        builder.HasIndex(c => c.SaleId).IsUnique().HasDatabaseName("ux_sale_commissions_sale_id");
        builder.HasIndex(c => c.DealerId).HasDatabaseName("ix_sale_commissions_dealer_id");
        // Backs the B4.2 reports/commissions query (per salesperson, per dealer).
        builder.HasIndex(c => new { c.DealerId, c.SalespersonId }).HasDatabaseName("ix_sale_commissions_dealer_id_salesperson_id");
    }
}

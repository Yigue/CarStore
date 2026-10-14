using Domain.Deposits;
using Infrastructure.Persistence.Configurations.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Database.Configurations;

/// <summary>
/// sfc B1.3.4 / ADR C4: the seña as its own tenant-scoped table. RN-004 is backed by the partial unique index
/// <c>ux_deposits_one_active_per_car</c>, so two concurrent requests cannot both leave an Active deposit on one car.
/// </summary>
internal sealed class DepositConfiguration : IEntityTypeConfiguration<Deposit>
{
    public void Configure(EntityTypeBuilder<Deposit> builder)
    {
        builder.ToTable("deposits");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.DealerId).IsRequired();
        builder.Property(d => d.CarId).IsRequired();
        builder.OwnsRequiredMoney(d => d.Amount, "amount", "amount_currency");
        builder.OwnsOptionalMoney(d => d.RefundedAmount, "refunded_amount", "refunded_amount_currency");
        builder.OwnsOptionalExchangeRate(d => d.ExchangeRate);
        builder.Property(d => d.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(d => d.RefundReason).HasMaxLength(500);
        builder.Property(d => d.CreatedAtUtc).IsRequired();
        builder.Property(d => d.ExpiresAtUtc).IsRequired();

        builder.Ignore(d => d.DomainEvents);

        builder.HasIndex(d => d.CarId)
            .IsUnique()
            .HasFilter("status = 'Active'")
            .HasDatabaseName("ux_deposits_one_active_per_car");
        // ExpireDepositsJob scans Active deposits by expiry across tenants.
        builder.HasIndex(d => new { d.Status, d.ExpiresAtUtc })
            .HasDatabaseName("ix_deposits_status_expires_at_utc");
        builder.HasIndex(d => d.DealerId).HasDatabaseName("ix_deposits_dealer_id");
    }
}

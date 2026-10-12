using Domain.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Database.Configurations;

/// <summary>
/// Billing history kept after the tenant purge: no foreign key to the dealer or the subscription row, and no
/// tenant query filter (it is written by the webhook job, which has no tenant context).
/// </summary>
internal sealed class BillingRefundRecordConfiguration : IEntityTypeConfiguration<BillingRefundRecord>
{
    public void Configure(EntityTypeBuilder<BillingRefundRecord> builder)
    {
        builder.ToTable("billing_refund_records");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.DealerId).IsRequired();
        builder.Property(r => r.SubscriptionId).IsRequired();
        builder.Property(r => r.ProviderEventId).HasMaxLength(255).IsRequired();
        builder.Property(r => r.ChargeId).HasMaxLength(255).IsRequired();
        builder.Property(r => r.InvoiceId).HasMaxLength(255);
        builder.Property(r => r.Currency).HasMaxLength(10).IsRequired();
        builder.Property(r => r.Reason).HasMaxLength(100);
        builder.Property(r => r.Kind).HasConversion<string>().HasMaxLength(10).IsRequired();
        builder.Property(r => r.OccurredAtUtc).IsRequired();
        builder.Property(r => r.RecordedAtUtc).IsRequired();

        builder.Ignore(r => r.DomainEvents);

        builder.HasIndex(r => r.ProviderEventId).IsUnique().HasDatabaseName(BillingRefundRecord.ProviderEventUniqueIndex);
        builder.HasIndex(r => new { r.DealerId, r.OccurredAtUtc }).HasDatabaseName("ix_billing_refund_records_dealer_id_occurred_at_utc");
    }
}

using Domain.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Billing;

public class BillingReconciliationFlagConfiguration : IEntityTypeConfiguration<BillingReconciliationFlag>
{
    public void Configure(EntityTypeBuilder<BillingReconciliationFlag> builder)
    {
        builder.ToTable("billing_reconciliation_flags");

        builder.HasKey(x => x.DealerId);

        builder.Property(x => x.DealerId)
            .HasColumnName("dealer_id")
            .ValueGeneratedNever();

        builder.Property(x => x.Reason)
            .HasColumnName("reason")
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.FlaggedAtUtc)
            .HasColumnName("flagged_at")
            .IsRequired();

        builder.Property(x => x.ClearedAtUtc)
            .HasColumnName("cleared_at")
            .IsRequired(false);

        builder.Ignore(x => x.IsOpen);
    }
}

using Domain.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Billing;

public class ProcessedStripeEventConfiguration : IEntityTypeConfiguration<ProcessedStripeEvent>
{
    public void Configure(EntityTypeBuilder<ProcessedStripeEvent> builder)
    {
        builder.ToTable("processed_stripe_events");

        builder.HasKey(x => x.StripeEventId);

        builder.Property(x => x.StripeEventId)
            .HasColumnName("stripe_event_id")
            .HasMaxLength(64);

        builder.Property(x => x.ProcessedOnUtc)
            .HasColumnName("processed_on_utc")
            .IsRequired();

        builder.Property(x => x.DealerId)
            .HasColumnName("dealer_id")
            .IsRequired(false);
    }
}

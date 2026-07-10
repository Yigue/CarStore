using Domain.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Billing;

public class DealerSubscriptionConfiguration : IEntityTypeConfiguration<DealerSubscription>
{
    public void Configure(EntityTypeBuilder<DealerSubscription> builder)
    {
        builder.ToTable("dealer_subscriptions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.Property(x => x.DealerId)
            .HasColumnName("dealer_id")
            .IsRequired();

        builder.HasIndex(x => x.DealerId)
            .IsUnique()
            .HasDatabaseName("ux_dealer_subscriptions_dealer_id");

        builder.Property(x => x.StripeCustomerId)
            .HasColumnName("stripe_customer_id")
            .HasMaxLength(64)
            .IsRequired(false);

        builder.Property(x => x.StripeSubscriptionId)
            .HasColumnName("stripe_subscription_id")
            .HasMaxLength(64)
            .IsRequired(false);

        builder.HasIndex(x => x.StripeSubscriptionId)
            .HasDatabaseName("ix_dealer_subscriptions_stripe_subscription_id");

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.HasIndex(x => x.Status)
            .HasDatabaseName("ix_dealer_subscriptions_status");

        builder.Property(x => x.TrialEndsAt)
            .HasColumnName("trial_ends_at")
            .IsRequired(false);

        builder.Property(x => x.CurrentPeriodStart)
            .HasColumnName("current_period_start")
            .IsRequired();

        builder.Property(x => x.CurrentPeriodEnd)
            .HasColumnName("current_period_end")
            .IsRequired();

        builder.Property(x => x.CancelledAt)
            .HasColumnName("cancelled_at")
            .IsRequired(false);

        builder.Property(x => x.PlanId)
            .HasColumnName("plan_id")
            .HasMaxLength(64)
            .IsRequired();
    }
}

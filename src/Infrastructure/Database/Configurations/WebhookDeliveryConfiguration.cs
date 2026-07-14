using Domain.Webhooks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Database.Configurations;

internal sealed class WebhookDeliveryConfiguration : IEntityTypeConfiguration<WebhookDelivery>
{
    public void Configure(EntityTypeBuilder<WebhookDelivery> builder)
    {
        builder.ToTable("webhook_deliveries");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.DealerId).IsRequired();
        builder.Property(x => x.SubscriptionId).IsRequired();
        builder.Property(x => x.EventId).IsRequired();

        builder.Property(x => x.EventType)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Payload)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(x => x.AttemptCount).IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.NextRetryAtUtc).IsRequired();
        builder.Property(x => x.LastStatusCode);
        builder.Property(x => x.LastError).HasMaxLength(2000);
        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.DeliveredAtUtc);

        builder.HasOne<WebhookSubscription>()
            .WithMany()
            .HasForeignKey(x => x.SubscriptionId)
            .OnDelete(DeleteBehavior.Cascade);

        // Idempotency guard: the outbox processor checks this pair before enqueueing so a
        // re-read of an in-flight OutboxMessage never creates a duplicate delivery row.
        builder.HasIndex(x => new { x.SubscriptionId, x.EventId })
            .IsUnique()
            .HasDatabaseName("ux_webhook_deliveries_subscription_event");

        // Dispatcher poll query: due deliveries across all tenants.
        builder.HasIndex(x => new { x.Status, x.NextRetryAtUtc })
            .HasDatabaseName("ix_webhook_deliveries_status_next_retry");
    }
}

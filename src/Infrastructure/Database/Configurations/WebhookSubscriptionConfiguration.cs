using Domain.Webhooks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Newtonsoft.Json;

namespace Infrastructure.Database.Configurations;

internal sealed class WebhookSubscriptionConfiguration : IEntityTypeConfiguration<WebhookSubscription>
{
    public void Configure(EntityTypeBuilder<WebhookSubscription> builder)
    {
        builder.ToTable("webhook_subscriptions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.DealerId).IsRequired();

        builder.Property(x => x.Url)
            .HasMaxLength(2048)
            .IsRequired();

        builder.Property(x => x.Secret)
            .HasMaxLength(256)
            .IsRequired();

        // Collection of catalog event names, persisted as a JSON array. Reassigning the
        // whole list (as UpdateDetails does) gives EF's default reference-equality change
        // tracking everything it needs — no custom ValueComparer required for this
        // create/replace-only usage.
        builder.Property(x => x.EventTypes)
            .HasConversion(
                v => JsonConvert.SerializeObject(v),
                v => JsonConvert.DeserializeObject<List<string>>(v) ?? new List<string>())
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(x => x.IsActive).IsRequired();
        builder.Property(x => x.CreatedAtUtc).IsRequired();

        builder.HasIndex(x => x.DealerId)
            .HasDatabaseName("ix_webhook_subscriptions_dealer_id");
    }
}

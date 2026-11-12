using Domain.Marketing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Database.Configurations;

/// <summary>sfc B5.1 / RN-019: newsletter subscribers, one row per (dealer, normalized email); tokens stored as SHA-256.</summary>
internal sealed class SubscriberConfiguration : IEntityTypeConfiguration<Subscriber>
{
    public void Configure(EntityTypeBuilder<Subscriber> builder)
    {
        builder.ToTable("subscribers");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.DealerId).IsRequired();
        builder.Property(s => s.Email).HasMaxLength(Subscriber.MaxEmailLength).IsRequired();
        builder.Property(s => s.EmailNormalized).HasMaxLength(Subscriber.MaxEmailLength).IsRequired();
        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(s => s.Source).HasMaxLength(50).IsRequired();
        builder.Property(s => s.ConfirmTokenHash).HasMaxLength(64);
        builder.Property(s => s.UnsubscribeTokenHash).HasMaxLength(64).IsRequired();
        builder.Ignore(s => s.DomainEvents);
        builder.HasIndex(s => new { s.DealerId, s.EmailNormalized }).IsUnique().HasDatabaseName("ux_subscribers_dealer_id_email_normalized");
        builder.HasIndex(s => s.ConfirmTokenHash).IsUnique().HasDatabaseName("ux_subscribers_confirm_token_hash");
        builder.HasIndex(s => s.UnsubscribeTokenHash).IsUnique().HasDatabaseName("ux_subscribers_unsubscribe_token_hash");
    }
}

using Domain.Leads;
using Infrastructure.Persistence.Configurations.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Database.Configurations;

internal sealed class LeadConfiguration : IEntityTypeConfiguration<Lead>
{
    public void Configure(EntityTypeBuilder<Lead> builder)
    {
        builder.ToTable("leads");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.DealerId).IsRequired();

        builder.Property(l => l.ClientName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(l => l.Email)
            .HasConversion(new EmailValueConverter())
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(l => l.Phone)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(l => l.Status)
            .HasConversion<string>()
            .HasMaxLength(30)
            .HasDefaultValue(LeadStatus.Nuevo)
            .IsRequired();

        // sfc C0.1 / RN-045: first-touch attribution as optional owned columns on leads.
        builder.OwnsOne(l => l.Attribution, a =>
        {
            const int max = Domain.Leads.LeadAttribution.MaxLength;
            a.Property(x => x.UtmSource).HasColumnName("attribution_utm_source").HasMaxLength(max);
            a.Property(x => x.UtmMedium).HasColumnName("attribution_utm_medium").HasMaxLength(max);
            a.Property(x => x.UtmCampaign).HasColumnName("attribution_utm_campaign").HasMaxLength(max);
            a.Property(x => x.UtmTerm).HasColumnName("attribution_utm_term").HasMaxLength(max);
            a.Property(x => x.UtmContent).HasColumnName("attribution_utm_content").HasMaxLength(max);
            a.Property(x => x.Referrer).HasColumnName("attribution_referrer").HasMaxLength(max);
            a.Property(x => x.LandingPath).HasColumnName("attribution_landing_path").HasMaxLength(max);
            a.Property(x => x.Gclid).HasColumnName("attribution_gclid").HasMaxLength(max);
            a.Property(x => x.Fbclid).HasColumnName("attribution_fbclid").HasMaxLength(max);
            a.Property(x => x.FirstTouchAtUtc).HasColumnName("attribution_first_touch_at_utc");
        });
        builder.Navigation(l => l.Attribution).IsRequired(false);

        builder.Property(l => l.Source)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(l => l.Notes)
            .HasMaxLength(2000);

        builder.Property(l => l.AssignedAgentId);

        builder.Property(l => l.CreatedAt).IsRequired();

        // CRM Integration fields
        builder.Property(l => l.InterestedVehicleId);

        builder.Property(l => l.ConvertedClientId);

        builder.Property(l => l.LossReason)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(l => l.ReengagementSentAtUtc);

        builder.HasOne<Domain.Cars.Car>()
            .WithMany()
            .HasForeignKey(l => l.InterestedVehicleId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasOne<Domain.Clients.Client>()
            .WithMany()
            .HasForeignKey(l => l.ConvertedClientId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasIndex(l => new { l.DealerId, l.Status })
            .HasDatabaseName("ix_leads_dealer_status");

        builder.HasIndex(l => new { l.DealerId, l.AssignedAgentId })
            .HasDatabaseName("ix_leads_dealer_agent");
    }
}

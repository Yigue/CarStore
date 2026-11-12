using Domain.Leads;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Integrations.Meta;

/// <summary>sfc C1.4 / B13: EF mapping for the Meta Leads Ads webhook idempotency table.</summary>
public sealed class ProcessedMetaLeadEventConfiguration : IEntityTypeConfiguration<ProcessedMetaLeadEvent>
{
    public void Configure(EntityTypeBuilder<ProcessedMetaLeadEvent> builder)
    {
        builder.ToTable("processed_meta_lead_events");

        builder.HasKey(x => x.LeadgenId);

        builder.Property(x => x.LeadgenId)
            .HasColumnName("leadgen_id")
            .HasMaxLength(64);

        builder.Property(x => x.ProcessedOnUtc)
            .HasColumnName("processed_on_utc")
            .IsRequired();

        builder.Property(x => x.DealerId)
            .HasColumnName("dealer_id")
            .IsRequired(false);

        builder.Property(x => x.LeadId)
            .HasColumnName("lead_id")
            .IsRequired(false);
    }
}

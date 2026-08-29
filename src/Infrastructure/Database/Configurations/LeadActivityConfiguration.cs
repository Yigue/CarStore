using Domain.Leads;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Database.Configurations;

internal sealed class LeadActivityConfiguration : IEntityTypeConfiguration<LeadActivity>
{
    public void Configure(EntityTypeBuilder<LeadActivity> builder)
    {
        builder.ToTable("lead_activities");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.DealerId).IsRequired();

        builder.Property(a => a.LeadId).IsRequired();

        builder.Property(a => a.Type)
            .HasConversion<string>()
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(a => a.Description)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(a => a.RelatedEntityId);

        builder.Property(a => a.RelatedEntityType)
            .HasMaxLength(40);

        builder.Property(a => a.ActorUserId);

        builder.Property(a => a.OccurredAtUtc).IsRequired();

        // Cascade: the history describes this lead and means nothing without it — unlike the
        // commercial records that block a vehicle delete, an activity row is not evidence of
        // anything on its own.
        builder.HasOne<Lead>()
            .WithMany()
            .HasForeignKey(a => a.LeadId)
            .OnDelete(DeleteBehavior.Cascade);

        // The timeline is always read as "this lead, newest first".
        builder.HasIndex(a => new { a.LeadId, a.OccurredAtUtc });

        builder.HasIndex(a => a.DealerId);
    }
}

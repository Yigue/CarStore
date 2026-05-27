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

        builder.Property(l => l.Source)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(l => l.Notes)
            .HasMaxLength(2000);

        builder.Property(l => l.AssignedAgentId);

        builder.Property(l => l.CreatedAt).IsRequired();

        builder.HasIndex(l => new { l.DealerId, l.Status })
            .HasDatabaseName("ix_leads_dealer_status");

        builder.HasIndex(l => new { l.DealerId, l.AssignedAgentId })
            .HasDatabaseName("ix_leads_dealer_agent");
    }
}

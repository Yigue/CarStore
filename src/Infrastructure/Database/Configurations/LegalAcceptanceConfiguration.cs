using Domain.Legal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Database.Configurations;

internal sealed class LegalAcceptanceConfiguration : IEntityTypeConfiguration<LegalAcceptance>
{
    public void Configure(EntityTypeBuilder<LegalAcceptance> builder)
    {
        builder.ToTable("legal_acceptances");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.DealerId).IsRequired();
        builder.Property(a => a.UserId).IsRequired();

        builder.Property(a => a.DocumentKind)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(a => a.Version).HasMaxLength(10).IsRequired();
        builder.Property(a => a.ContentSha256).HasMaxLength(64).IsRequired();
        builder.Property(a => a.AcceptedAtUtc).IsRequired();
        builder.Property(a => a.Ip).HasMaxLength(64);
        builder.Property(a => a.UserAgent).HasMaxLength(512);

        // No FKs on purpose: the audit trail must survive user/dealer lifecycle changes.
        builder.HasIndex(a => new { a.UserId, a.DocumentKind, a.AcceptedAtUtc });
        builder.HasIndex(a => a.DealerId);
    }
}

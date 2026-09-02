using Domain.Documents;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Database.Configurations;

public sealed class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.ToTable("documents", Schemas.Default);
        
        builder.HasKey(x => x.Id);

        builder.Property(x => x.DealerId)
            .IsRequired();

        builder.Property(x => x.BlobName)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(x => x.FileName)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(x => x.ContentType)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.OcrStatus)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(x => x.Type)
            .IsRequired()
            .HasConversion<string>();

        // OcrExtractedData is a value-object record persisted inline as a JSON column.
        builder.OwnsOne(x => x.ParsedData, owned => owned.ToJson());

        builder.Property(x => x.OcrRawJson)
            .HasColumnType("jsonb");

        builder.Property(x => x.UploadedAtUtc).IsRequired();
        builder.Property(x => x.VerifiedAtUtc);
        builder.Property(x => x.DiscrepancyNotes).HasMaxLength(1000);

        // Backward-compatibility aliases on the entity must not be mapped as columns.
        builder.Ignore(x => x.Status);
        builder.Ignore(x => x.ExtractedData);
        builder.Ignore(x => x.BlobUrl);

        builder.HasOne(x => x.Client)
            .WithMany()
            .HasForeignKey(x => x.ClientId)
            .OnDelete(DeleteBehavior.SetNull);

        // No navigation property: Document lives in its own aggregate and only needs the id.
        // SetNull mirrors the client relationship — deleting a sale must not take the paperwork
        // with it.
        builder.HasOne<Domain.Sales.Sale>()
            .WithMany()
            .HasForeignKey(x => x.SaleId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        builder.HasIndex(x => x.DealerId);
        builder.HasIndex(x => x.ClientId);
        builder.HasIndex(x => x.SaleId);
    }
}

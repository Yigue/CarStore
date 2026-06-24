using Domain.Cars;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Database.Configurations;

internal sealed class CarImageConfiguration : IEntityTypeConfiguration<CarImage>
{
    public void Configure(EntityTypeBuilder<CarImage> builder)
    {
        builder.ToTable("car_images");

        builder.HasKey(ci => ci.Id);

        // Legacy direct URL — now nullable (new images use ObjectKey instead). ADR-2 dual-mode.
        builder.Property(ci => ci.ImageUrl)
            .HasMaxLength(500)
            .IsRequired(false);

        // MinIO object key: cars/{dealerId}/{carId}/{imageId}.{ext}
        builder.Property(ci => ci.ObjectKey)
            .HasMaxLength(1024)
            .IsRequired(false);

        builder.Property(ci => ci.ContentType)
            .HasMaxLength(100)
            .IsRequired(false);

        builder.Property(ci => ci.SizeBytes)
            .IsRequired(false);

        builder.Property(ci => ci.IsCover)
            .HasDefaultValue(false);

        builder.Property(ci => ci.DisplayOrder)
            .HasDefaultValue(0);

        // Ignore backward-compat read-only aliases (they map to IsCover / DisplayOrder).
        builder.Ignore(ci => ci.IsPrimary);
        builder.Ignore(ci => ci.Order);

        builder.HasOne(ci => ci.Car)
            .WithMany(c => c.Images)
            .HasForeignKey(ci => ci.CarId)
            .OnDelete(DeleteBehavior.Cascade);

        // Gallery ordering / lookup by car.
        builder.HasIndex(ci => new { ci.CarId, ci.DisplayOrder });

        // REQ-VMS-7: at most one cover per car. The partial UNIQUE index (filter "is_cover = true")
        // is added provider-conditionally in ApplicationDbContext.OnModelCreating — only for
        // Npgsql — because the filtered-index SQL is not portable to the SQLite EnsureCreated()
        // path used by integration tests. Cover exclusivity is additionally guaranteed by
        // SetCoverImageCommandHandler.
    }
}

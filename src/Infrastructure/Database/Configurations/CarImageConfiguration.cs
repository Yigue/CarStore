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

        builder.Property(ci => ci.ImageUrl)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(ci => ci.IsPrimary)
            .HasDefaultValue(false);

        builder.Property(ci => ci.Order)
            .HasDefaultValue(0);

        builder.HasOne(ci => ci.Car)
            .WithMany(c => c.Images)
            .HasForeignKey(ci => ci.CarId)
            .OnDelete(DeleteBehavior.Cascade);
    }
} 
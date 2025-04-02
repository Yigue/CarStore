using Domain.Cars;
using Domain.Cars.Atribbutes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations.Cars;

public class CarConfiguration : IEntityTypeConfiguration<Car>
{
    public void Configure(EntityTypeBuilder<Car> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Patente)
            .HasMaxLength(10)
            .IsRequired();

        builder.HasIndex(c => c.Patente)
            .IsUnique();

        builder.Property(c => c.Descripcion)
            .HasMaxLength(500);

        builder.Property(c => c.Kilometraje)
            .IsRequired();

        builder.Property(c => c.Año)
            .IsRequired();

        builder.HasOne(c => c.Marca)
            .WithMany()
            .HasForeignKey("MarcaId")
            .IsRequired();

        builder.HasOne(c => c.Modelo)
            .WithMany()
            .HasForeignKey("ModeloId")
            .IsRequired();

        builder.Property(c => c.CarType)
            .HasConversion<string>();

        builder.Property(c => c.CarStatus)
            .HasConversion<string>();

        builder.Property(c => c.ServiceCar)
            .HasConversion<string>();

        builder.Property(c => c.FuelType)
            .HasConversion<string>();

  
        builder.Property(c => c.Price)
           .IsRequired();


    }
}

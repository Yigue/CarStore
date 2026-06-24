using Domain.Cars;
using Domain.Cars.Attributes;
using Infrastructure.Persistence.Configurations.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations.Cars;

public class CarConfiguration : IEntityTypeConfiguration<Car>
{
    public void Configure(EntityTypeBuilder<Car> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Patente)
            .HasConversion(new LicensePlateValueConverter())
            .HasMaxLength(10)
            .IsRequired();

        builder.HasIndex(c => c.Patente)
            .IsUnique();

        builder.Property(c => c.Descripcion)
            .HasMaxLength(500);

        builder.Property(c => c.Kilometraje)
            .IsRequired();

        builder.Property(c => c.Anio)
            .HasColumnName("año")
            .IsRequired();

        builder.HasOne(c => c.Marca)
            .WithMany()
            .HasForeignKey(c => c.MarcaId)
            .IsRequired();

        builder.HasOne(c => c.Modelo)
            .WithMany()
            .HasForeignKey(c => c.ModeloId)
            .IsRequired();

        builder.Property(c => c.CarType)
            .HasConversion<string>();

        builder.Property(c => c.CarStatus)
            .HasConversion<string>();

        builder.Property(c => c.ServiceCar)
            .HasConversion<string>();

        builder.Property(c => c.FuelType)
            .HasConversion<string>();

        builder.Property(c => c.Transmission)
            .HasConversion<string>()
            .HasDefaultValue(Transmission.Manual)
            .IsRequired();

        builder.Property(c => c.Featured)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(c => c.Price)
            .HasConversion(new MoneyValueConverter())
            .HasColumnName("price")
            .IsRequired();

        // PHASE-4: PurchaseCost mapped as owned value object (Amount + Currency columns).
        // The OWNED REFERENCE is optional (navigation IsRequired(false)) so the whole
        // pair of columns is nullable in the DB. Existing Cars survive without backfill.
        builder.OwnsOne(c => c.PurchaseCost, pc =>
        {
            pc.Property(p => p.Amount)
                .HasColumnName("purchase_cost_amount")
                .HasColumnType("decimal(18,2)");

            pc.Property(p => p.Currency)
                .HasColumnName("purchase_cost_currency")
                .HasMaxLength(3);
        });

        builder.Navigation(c => c.PurchaseCost).IsRequired(false);

        // PHASE-4: Bind EF Core collection navigation to the private backing field so
        // the aggregate keeps exposing IReadOnlyList<ReconditioningTask>.
        builder.Metadata
            .FindNavigation(nameof(Car.ReconditioningTasks))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}

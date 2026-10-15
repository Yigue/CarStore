using Domain.Cars;
using Infrastructure.Persistence.Configurations.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Database.Configurations;

/// <summary>sfc B7.2 / RN-037: liens over vehicles, tenant-scoped.</summary>
internal sealed class VehicleLienConfiguration : IEntityTypeConfiguration<VehicleLien>
{
    public void Configure(EntityTypeBuilder<VehicleLien> builder)
    {
        builder.ToTable("vehicle_liens");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.DealerId).IsRequired();
        builder.Property(l => l.CarId).IsRequired();
        builder.Property(l => l.Creditor).HasMaxLength(200).IsRequired();
        builder.OwnsRequiredMoney(l => l.Amount, "amount", "amount_currency");
        builder.Property(l => l.RegistrationNumber).HasMaxLength(100);
        builder.Property(l => l.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(l => l.RegisteredAtUtc).IsRequired();
        builder.Ignore(l => l.DomainEvents);
        builder.HasIndex(l => new { l.CarId, l.Status }).HasDatabaseName("ix_vehicle_liens_car_id_status");
        builder.HasIndex(l => l.DealerId).HasDatabaseName("ix_vehicle_liens_dealer_id");
    }
}

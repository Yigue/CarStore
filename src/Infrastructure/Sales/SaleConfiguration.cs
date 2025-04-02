using Domain.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations.Sales;

public class SaleConfiguration : IEntityTypeConfiguration<Sale>
{
    public void Configure(EntityTypeBuilder<Sale> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.FinalPrice)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(s => s.Status)
            .HasConversion<string>();

        builder.Property(s => s.PaymentMethod)
            .HasConversion<string>();

        builder.Property(s => s.ContractNumber)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(s => s.Comments)
            .HasMaxLength(500);

        builder.Property(s => s.SaleDate)
            .IsRequired();

        builder.HasOne(s => s.Car)
            .WithMany()
            .HasForeignKey(s => s.CarId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.Client)
            .WithMany(c => c.Sales)
            .HasForeignKey(s => s.ClientId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

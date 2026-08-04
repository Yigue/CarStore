using Domain.Sales;
using Infrastructure.Persistence.Configurations.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations.Sales;

public class SaleConfiguration : IEntityTypeConfiguration<Sale>
{
    public void Configure(EntityTypeBuilder<Sale> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.FinalPrice)
            .HasConversion(new MoneyValueConverter())
            .HasColumnName("final_price")
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

        builder.Property(s => s.QuoteId);

        builder.Property(s => s.LeadId);

        // No hard FK to Users — mirrors Lead.AssignedAgentId / Client.AssignedAgentId
        // convention (agent references stay a plain column + index, not a constrained FK).
        builder.Property(s => s.SalespersonId)
            .HasColumnName("salesperson_id");

        builder.HasIndex(s => s.SalespersonId)
            .HasDatabaseName("ix_sales_salesperson_id");

        builder.HasOne(s => s.Car)
            .WithMany()
            .HasForeignKey(s => s.CarId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.Client)
            .WithMany(c => c.Sales)
            .HasForeignKey(s => s.ClientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Domain.Quotes.Quote>()
            .WithMany()
            .HasForeignKey(s => s.QuoteId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasOne<Domain.Leads.Lead>()
            .WithMany()
            .HasForeignKey(s => s.LeadId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasIndex(s => s.CarId)
            .IsUnique()
            .HasDatabaseName("ux_sales_one_completed_per_car")
            .HasFilter("status = 'Completed'");
    }
}

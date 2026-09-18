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

        // ─── VEN-03 · payment breakdown ───────────────────────────────────────────────────────
        // Every column is nullable: a sale recorded before this change, or one paid in cash,
        // has nothing to fill in here.

        builder.Property(s => s.DownPayment)
            .HasConversion(new NullableMoneyValueConverter())
            .HasColumnName("down_payment");

        builder.Property(s => s.TradeInCarId)
            .HasColumnName("trade_in_car_id");

        builder.Property(s => s.TradeInValue)
            .HasConversion(new NullableMoneyValueConverter())
            .HasColumnName("trade_in_value");

        builder.Property(s => s.FinancedAmount)
            .HasConversion(new NullableMoneyValueConverter())
            .HasColumnName("financed_amount");

        builder.Property(s => s.InstallmentCount)
            .HasColumnName("installment_count");

        builder.Property(s => s.InstallmentAmount)
            .HasConversion(new NullableMoneyValueConverter())
            .HasColumnName("installment_amount");

        builder.Property(s => s.FinancingEntity)
            .HasMaxLength(120)
            .HasColumnName("financing_entity");

        // The trade-in points at a Car but is NOT a constrained FK: the unit taken in part
        // payment may be entered into inventory later, or not at all, and a hard constraint
        // would block recording the sale until someone created it. Same convention as
        // SalespersonId above.
        builder.HasIndex(s => s.TradeInCarId)
            .HasDatabaseName("ix_sales_trade_in_car_id");

        // ─── VEN-03 · legal paperwork ─────────────────────────────────────────────────────────
        // Attachments already exist (Document.SaleId). A file is not a number: the factura, the
        // 08 and the patentamiento are looked up by theirs.

        builder.Property(s => s.InvoiceNumber)
            .HasMaxLength(50)
            .HasColumnName("invoice_number");

        builder.Property(s => s.TransferFormNumber)
            .HasMaxLength(50)
            .HasColumnName("transfer_form_number");

        builder.Property(s => s.RegistrationNumber)
            .HasMaxLength(50)
            .HasColumnName("registration_number");

        builder.Property(s => s.DeliveryDate)
            .HasColumnName("delivery_date");
    }
}

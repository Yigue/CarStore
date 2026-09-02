using Domain.Quotes;
using Infrastructure.Persistence.Configurations.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations.Quotes;

public class QuoteConfiguration : IEntityTypeConfiguration<Quote>
{
    public void Configure(EntityTypeBuilder<Quote> builder)
    {
        builder.HasKey(q => q.Id);

        builder.Property(q => q.ProposedPrice)
            .HasConversion(new MoneyValueConverter())
            .HasColumnName("proposed_price")
            .IsRequired();

        builder.Property(q => q.Status)
            .HasConversion<string>();

        builder.Property(q => q.PaymentMethod)
            .HasConversion<string>()
            .HasColumnName("payment_method")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(q => q.ValidUntil)
            .IsRequired();

        builder.Property(q => q.Comments)
            .HasMaxLength(500);

        builder.Property(q => q.CreatedAt)
            .IsRequired();

        builder.Property(q => q.IsDeleted)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(q => q.DeletedAtUtc);

        builder.HasIndex(q => q.IsDeleted);

        builder.HasOne(q => q.Car)
            .WithMany()
            .HasForeignKey(q => q.CarId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(q => q.Client)
            .WithMany()
            .HasForeignKey(q => q.ClientId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasOne(q => q.Lead)
            .WithMany()
            .HasForeignKey(q => q.LeadId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        // One commitment per car. A car may carry any number of competing offers, but accepting
        // one is the dealership committing the unit, and AcceptQuoteCommandHandler's check —
        // read the accepted quotes, then write — has no protection against two requests landing
        // between the read and the write. The same reason Sales carries
        // ux_sales_one_completed_per_car; this is its counterpart one step earlier in the flow.
        // Soft-deleted rows are excluded so a deleted acceptance frees the car for a new one.
        // Named overload: the plain lookup index has to survive alongside the unique partial
        // one below. Both handlers query quotes by car, and a filtered index covering only
        // Accepted rows answers none of those lookups.
        builder.HasIndex(q => q.CarId, "ix_quotes_car_id");

        builder.HasIndex(q => q.CarId, "ux_quotes_one_accepted_per_car")
            .IsUnique()
            .HasDatabaseName("ux_quotes_one_accepted_per_car")
            .HasFilter("status = 'Accepted' AND is_deleted = false");
    }
}

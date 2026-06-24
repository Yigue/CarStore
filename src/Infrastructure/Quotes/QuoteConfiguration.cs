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
    }
}

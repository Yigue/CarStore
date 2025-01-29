using Domain.Quotes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations.Quotes;

public class QuoteConfiguration : IEntityTypeConfiguration<Quote>
{
    public void Configure(EntityTypeBuilder<Quote> builder)
    {
        builder.HasKey(q => q.Id);

        builder.Property(q => q.ProposedPrice)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(q => q.Status)
            .HasConversion<string>();

        builder.Property(q => q.ValidUntil)
            .IsRequired();

        builder.Property(q => q.Comments)
            .HasMaxLength(500);

        builder.Property(q => q.CreatedAt)
            .IsRequired();

        builder.HasOne(q => q.Car)
            .WithMany()
            .HasForeignKey(q => q.CarId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(q => q.Client)
            .WithMany()
            .HasForeignKey(q => q.ClientId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

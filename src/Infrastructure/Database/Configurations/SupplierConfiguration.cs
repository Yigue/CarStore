using Domain.Suppliers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Database.Configurations;

/// <summary>sfc B7.4 / RN-039: suppliers per dealer, name unique case-insensitively through the normalized column.</summary>
internal sealed class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> builder)
    {
        builder.ToTable("suppliers");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.DealerId).IsRequired();
        builder.Property(s => s.Name).HasMaxLength(Supplier.MaxNameLength).IsRequired();
        builder.Property(s => s.NameNormalized).HasMaxLength(Supplier.MaxNameLength).IsRequired();
        builder.Property(s => s.ContactEmail).HasMaxLength(200);
        builder.Property(s => s.Phone).HasMaxLength(50);
        builder.Ignore(s => s.DomainEvents);
        builder.HasIndex(s => new { s.DealerId, s.NameNormalized }).IsUnique().HasDatabaseName("ux_suppliers_dealer_id_name_normalized");
    }
}

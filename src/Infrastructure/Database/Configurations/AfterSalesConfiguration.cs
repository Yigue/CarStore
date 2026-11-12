using Domain.AfterSales;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Database.Configurations;

/// <summary>sfc C2.3 / RN-049: one warranty per sale.</summary>
internal sealed class WarrantyRecordConfiguration : IEntityTypeConfiguration<WarrantyRecord>
{
    public void Configure(EntityTypeBuilder<WarrantyRecord> builder)
    {
        builder.ToTable("warranty_records");
        builder.HasKey(w => w.Id);
        builder.Property(w => w.DealerId).IsRequired();
        builder.Property(w => w.Conditions).HasMaxLength(1000);
        builder.Ignore(w => w.DomainEvents);
        builder.HasIndex(w => w.SaleId).IsUnique().HasDatabaseName("ux_warranty_records_sale_id");
        builder.HasIndex(w => new { w.DealerId, w.EndsOnUtc }).HasDatabaseName("ix_warranty_records_dealer_ends_on");
    }
}

/// <summary>sfc C2.4 / RN-049: one reminder per sale, kind and due date, so the sweep can run again safely.</summary>
internal sealed class ServiceReminderConfiguration : IEntityTypeConfiguration<ServiceReminder>
{
    public void Configure(EntityTypeBuilder<ServiceReminder> builder)
    {
        builder.ToTable("service_reminders");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.DealerId).IsRequired();
        builder.Property(r => r.Kind).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Ignore(r => r.DomainEvents);
        builder.HasIndex(r => new { r.SaleId, r.Kind, r.DueOnUtc }).IsUnique().HasDatabaseName("ux_service_reminders_sale_kind_due");
        builder.HasIndex(r => new { r.DealerId, r.DueOnUtc }).HasDatabaseName("ix_service_reminders_dealer_due");
    }
}

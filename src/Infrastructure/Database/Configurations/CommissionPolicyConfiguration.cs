using Domain.Commissions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Database.Configurations;

/// <summary>sfc B4.1 / RN-009: one commission policy row per dealer; per-salesperson overrides travel as JSON.</summary>
internal sealed class CommissionPolicyConfiguration : IEntityTypeConfiguration<CommissionPolicy>
{
    public void Configure(EntityTypeBuilder<CommissionPolicy> builder)
    {
        builder.ToTable("commission_policies");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.DealerId).IsRequired();
        builder.Property(p => p.DefaultCommissionRate).HasPrecision(5, 2).IsRequired();
        builder.Property<string?>("OverridesJson").HasColumnName("overrides_json").HasColumnType("text");

        builder.Ignore(p => p.DomainEvents);

        builder.HasIndex(p => p.DealerId).IsUnique().HasDatabaseName("ux_commission_policies_dealer_id");
    }
}

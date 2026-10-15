using Domain.Branches;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Database.Configurations;

/// <summary>sfc C2.1 / RN-048: branches per dealer, name unique case-insensitively.</summary>
internal sealed class BranchConfiguration : IEntityTypeConfiguration<Branch>
{
    public void Configure(EntityTypeBuilder<Branch> builder)
    {
        builder.ToTable("branches");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.DealerId).IsRequired();
        builder.Property(b => b.Name).HasMaxLength(Branch.MaxNameLength).IsRequired();
        builder.Property(b => b.NameNormalized).HasMaxLength(Branch.MaxNameLength).IsRequired();
        builder.Ignore(b => b.DomainEvents);
        builder.HasIndex(b => new { b.DealerId, b.NameNormalized }).IsUnique().HasDatabaseName("ux_branches_dealer_id_name_normalized");
    }
}

/// <summary>sfc C2.1: which users work at which branch.</summary>
internal sealed class UserBranchConfiguration : IEntityTypeConfiguration<UserBranch>
{
    public void Configure(EntityTypeBuilder<UserBranch> builder)
    {
        builder.ToTable("user_branches");
        builder.HasKey(ub => new { ub.UserId, ub.BranchId });
        builder.HasIndex(ub => ub.BranchId).HasDatabaseName("ix_user_branches_branch_id");
    }
}

using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Database.Configurations;

/// <summary>sfc B6.3 / RN-043: one row per extra dealer a user can switch into; unique per (user, dealer).</summary>
internal sealed class UserDealerMembershipConfiguration : IEntityTypeConfiguration<UserDealerMembership>
{
    public void Configure(EntityTypeBuilder<UserDealerMembership> builder)
    {
        builder.ToTable("user_dealer_memberships");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.DealerId).IsRequired();
        builder.Property(m => m.UserId).IsRequired();
        builder.Property(m => m.RoleId).IsRequired();
        builder.Property(m => m.GrantedAtUtc).IsRequired();
        builder.Ignore(m => m.DomainEvents);
        builder.HasIndex(m => new { m.UserId, m.DealerId }).IsUnique().HasDatabaseName("ux_user_dealer_memberships_user_id_dealer_id");
        builder.HasIndex(m => m.UserId).HasDatabaseName("ix_user_dealer_memberships_user_id");
    }
}

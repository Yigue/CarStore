using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Database.Configurations;

internal sealed class UserPermissionConfiguration : IEntityTypeConfiguration<UserPermission>
{
    public void Configure(EntityTypeBuilder<UserPermission> builder)
    {
        builder.ToTable("UserPermissions");
        
        builder.HasKey(x => x.Id);

        builder.Property(x => x.UserId)
            .IsRequired();

        builder.Property(x => x.Permission)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.GrantedAt)
            .IsRequired();

        builder.Property(x => x.GrantedBy)
            .IsRequired(false);

        builder.HasOne(x => x.User)
            .WithMany(u => u.Permissions)
            .HasForeignKey(x => x.UserId);

        builder.HasIndex(x => new { x.UserId, x.Permission })
            .IsUnique();
    }
}

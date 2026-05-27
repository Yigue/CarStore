using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Database.Configurations;

internal sealed class UserPermissionConfiguration : IEntityTypeConfiguration<UserPermission>
{
    public void Configure(EntityTypeBuilder<UserPermission> builder)
    {
        // No ToTable here to respect snake_case convention in prod 
        // and default naming in tests, unless convention is applied.
        
        builder.HasKey(x => x.Id);

        builder.Property(x => x.UserId)
            .IsRequired();

        builder.Property(x => x.Permission)
            .HasMaxLength(100)
            .IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId);
    }
}

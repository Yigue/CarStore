using Domain.Users;
using Infrastructure.Persistence.Configurations.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Database.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Email)
            .HasConversion(new EmailValueConverter());

        builder.HasIndex(u => u.Email).IsUnique();

        builder.Property(u => u.FirstName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(u => u.LastName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(u => u.PasswordHash)
            .IsRequired();

        // Persistir Role como string para legibilidad en DB
        // y evitar problemas al renumerar el enum.
        builder.Property(u => u.Role)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(u => u.IsActive)
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(u => u.Phone)
            .HasMaxLength(20)
            .IsRequired(false);

        builder.Property(u => u.CreatedAt)
            .IsRequired();
    }
}

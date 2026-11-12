using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Database.Configurations;

internal sealed class UserActionTokenConfiguration : IEntityTypeConfiguration<UserActionToken>
{
    public void Configure(EntityTypeBuilder<UserActionToken> builder)
    {
        builder.ToTable("user_action_tokens");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Purpose)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        // SHA-256 hex digest. The secret itself is never stored.
        builder.Property(t => t.TokenHash)
            .HasMaxLength(64)
            .IsRequired();
        builder.HasIndex(t => t.TokenHash).IsUnique();

        builder.Property(t => t.Email)
            .HasMaxLength(320)
            .IsRequired();

        builder.Property(t => t.DealerId).IsRequired();
        builder.Property(t => t.UserId).IsRequired(false);
        builder.Property(t => t.RoleId).IsRequired(false);
        builder.Property(t => t.CreatedBy).IsRequired(false);
        builder.Property(t => t.CreatedAtUtc).IsRequired();
        builder.Property(t => t.ExpiresAtUtc).IsRequired();
        builder.Property(t => t.ConsumedAtUtc).IsRequired(false);

        // Supports "revoke the pending tokens of the same (purpose, email)".
        builder.HasIndex(t => new { t.Purpose, t.Email });
    }
}

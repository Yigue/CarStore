using System.Text.Json;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Database.Configurations;

/// <summary>sfc B5.3 / ADR C21: one row per user, the document stored as jsonb; removed with its user.</summary>
internal sealed class UserPreferencesConfiguration : IEntityTypeConfiguration<UserPreferences>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public void Configure(EntityTypeBuilder<UserPreferences> builder)
    {
        builder.ToTable("user_preferences");
        builder.HasKey(p => p.UserId);

        builder.Property(p => p.SchemaVersion).IsRequired();
        builder.Property(p => p.Preferences)
            .HasColumnName("preferences")
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, Json),
                v => JsonSerializer.Deserialize<PreferencesV1>(v, Json) ?? PreferencesV1.Default,
                new ValueComparer<PreferencesV1>((a, b) => Equals(a, b), v => v.GetHashCode(), v => v))
            .IsRequired();

        builder.HasOne<User>()
            .WithOne()
            .HasForeignKey<UserPreferences>(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

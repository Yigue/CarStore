using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DealerSettingsEntity = Domain.DealerSettings.DealerSettings;

namespace Infrastructure.Database.Configurations;

internal sealed class DealerSettingsConfiguration : IEntityTypeConfiguration<DealerSettingsEntity>
{
    public void Configure(EntityTypeBuilder<DealerSettingsEntity> builder)
    {
        builder.ToTable("dealer_settings");

        builder.HasKey(s => s.Id);

        // Una fila por dealer.
        builder.HasIndex(s => s.DealerId).IsUnique();

        // Subdomain uniqueness — DB is the source of truth (REQ: concurrent
        // provisioning cannot race past an app-level check). PostgreSQL partial
        // unique index ignores rows with NULL HostName so legacy seed rows
        // without a subdomain continue to work.
        //
        // NOTE (saas-custom-domains-followups item 2): this used to declare a
        // second, duplicate unique index here named "IX_DealerSettings_HostName_Unique"
        // (PascalCase, predates the project's snake_case index-naming convention).
        // It was redundant with the "ux_dealer_settings_host_name" index declared
        // further below (same column, same partial predicate) and has been dropped
        // via the DropDuplicateDealerSettingsHostNameIndex migration. Keep only the
        // single HasIndex(s => s.HostName) declaration below.

        builder.Property(s => s.DealerName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(s => s.ContactEmail)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(s => s.NotificationsEnabled)
            .HasDefaultValue(true);

        builder.Property(s => s.CreatedAt)
            .IsRequired();

        builder.Property(s => s.UpdatedAt)
            .IsRequired();

        builder.Property(s => s.LastAssignedAgentIndex)
            .HasDefaultValue(0)
            .IsRequired();

        // Platform suspension columns
        builder.Property(s => s.IsActive)
            .HasDefaultValue(true)
            .IsRequired();

        builder.HasIndex(s => s.IsActive)
            .HasDatabaseName("ix_dealer_settings_is_active");

        builder.Property(s => s.SuspendedAt)
            .IsRequired(false);

        builder.Property(s => s.SuspendReason)
            .HasMaxLength(500)
            .IsRequired(false);

        // RowVersion is intentionally NOT mapped here.
        // For Postgres: ApplicationDbContext.OnModelCreating maps it to the xmin system column
        // (concurrency token) in the Postgres-only block.
        // For InMemory / SQLite (tests): it is ignored so no extra column is created.
        // RowVersion will be 0 for all test entities (accepted; ETag = "v0").
        builder.Ignore(s => s.RowVersion);

        // Visual settings
        builder.Property(s => s.LogoUrl)
            .HasMaxLength(500)
            .IsRequired(false);

        builder.Property(s => s.PrimaryColor)
            .HasMaxLength(7)
            .IsRequired(false);

        builder.Property(s => s.SecondaryColor)
            .HasMaxLength(7)
            .IsRequired(false);

        builder.Property(s => s.FooterText)
            .HasMaxLength(200)
            .IsRequired(false);

        // ──────────────────────────────────────────────────────────────────────
        // PR1 (saas-custom-domains) tenant-identity hardening.
        //
        // HostName is the public DNS label the tenant serves on
        // (e.g. xyz.carstore.com). Must fit a fully-qualified hostname (≤253
        // chars, dotted). The unique + lookup indexes are hand-written in the
        // AddDealerSettingsHostNameUniqueIndex migration because partial
        // predicates are not portable to SQLite EnsureCreated (used by tests).
        //
        // Locked decisions applied here:
        // - O2: HostName stays nullable — NOT NULL is explicitly NOT enforced
        //   this change (tasks.md Locked Decision O2), at the EF level nor the
        //   DB level. Do not add a `SET NOT NULL` step to the schema migration
        //   without also revisiting this IsRequired(false) and O2 itself.
        // - O5: HostName itself is validated at the Domain layer
        //   (DealerSettings.ChangeSlug / ValidateFullyQualifiedHostName).
        // ──────────────────────────────────────────────────────────────────────
        builder.Property(s => s.HostName)
            .HasMaxLength(253)
            .IsRequired(false);

        // Slug: the public tenant identifier on {slug}.carstore.com.
        // Currently nullable at the DB level so legacy rows can survive the
        // backfill window; the NOT NULL + UNIQUE enforcement is a follow-up.
        builder.Property(s => s.Slug)
            .HasMaxLength(DealerSettingsEntity.TenantLabelMaxLength)
            .IsRequired(false);

        builder.Property(s => s.IsActive)
            .HasDefaultValue(true)
            .IsRequired();

        // EF Core declares a partial UNIQUE index for HostName. The schema
        // migration AddDealerSettingsHostNameUniqueIndex re-creates this index
        // via CREATE UNIQUE INDEX CONCURRENTLY … WHERE HostName IS NOT NULL
        // (and a parallel non-unique index on lower(host_name) for
        // case-insensitive lookup), but the EF model snapshot intentionally
        // mirrors the indexes so dotnet ef migrations script keeps them
        // idempotent.
        builder.HasIndex(s => s.HostName)
            .HasDatabaseName("ux_dealer_settings_host_name")
            .IsUnique()
            .HasFilter("host_name IS NOT NULL");

        builder.HasIndex(s => s.Slug)
            .HasDatabaseName("ux_dealer_settings_slug")
            .IsUnique()
            .HasFilter("slug IS NOT NULL");

        builder.HasIndex(s => new { s.HostName, s.IsActive })
            .HasDatabaseName("ix_dealer_settings_host_name_active_lookup")
            .IsUnique(false)
            .HasFilter("is_active = true");
    }
}

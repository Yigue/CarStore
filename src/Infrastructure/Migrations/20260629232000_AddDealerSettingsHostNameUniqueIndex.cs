using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <summary>
    /// PR1 (saas-custom-domains) constraint enforcement.
    /// <para>
    /// Depends on <c>BackfillDealerSettingsHostName</c> having populated every
    /// legacy row's <c>host_name</c> + <c>slug</c>. Once that succeeds:
    /// </para>
    /// <list type="bullet">
    ///   <item>Sets <c>host_name</c> + <c>slug</c> to <c>NOT NULL</c>.</item>
    ///   <item>
    ///     Creates partial UNIQUE indexes (one per column) using
    ///     <c>CREATE UNIQUE INDEX CONCURRENTLY</c> so the deploy does not lock
    ///     reads/writes on the table.
    ///   </item>
    ///   <item>
    ///     Creates a non-unique partial lookup index on
    ///     <c>(host_name) WHERE IsActive = true</c> so anonymous catalog
    ///     lookups do not perform a full table scan.
    ///   </item>
    /// </list>
    /// <para>
    /// Spec: <c>openspec/changes/saas-custom-domains/specs/wildcard-subdomain-routing</c>.
    /// </para>
    /// </summary>
    public partial class AddDealerSettingsHostNameUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Plain ALTER COLUMN (NOT NULL) — must run AFTER backfill.
            // If legacy rows are still NULL this throws and the operator can
            // re-run the backfill migration before retrying.
            migrationBuilder.Sql("ALTER TABLE public.dealer_settings ALTER COLUMN host_name SET NOT NULL;");
            migrationBuilder.Sql("ALTER TABLE public.dealer_settings ALTER COLUMN slug SET NOT NULL;");

            // CONCURRENTLY so the index build does not take an ACCESS EXCLUSIVE
            // lock on `dealer_settings`. PostgreSQL requires these statements
            // to run outside a transaction, which is why this migration is
            // separate from the backfill above (which IS transactional).
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX CONCURRENTLY IF NOT EXISTS ux_dealer_settings_host_name "
                + "ON public.dealer_settings (host_name) WHERE host_name IS NOT NULL;");

            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX CONCURRENTLY IF NOT EXISTS ux_dealer_settings_slug "
                + "ON public.dealer_settings (slug) WHERE slug IS NOT NULL;");

            migrationBuilder.Sql(
                "CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_dealer_settings_host_name_active "
                + "ON public.dealer_settings (host_name) WHERE is_active = true;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS public.ix_dealer_settings_host_name_active;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS public.ux_dealer_settings_slug;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS public.ux_dealer_settings_host_name;");

            migrationBuilder.Sql("ALTER TABLE public.dealer_settings ALTER COLUMN slug DROP NOT NULL;");
            migrationBuilder.Sql("ALTER TABLE public.dealer_settings ALTER COLUMN host_name DROP NOT NULL;");
        }
    }
}

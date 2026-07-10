using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <summary>
    /// PR1 (saas-custom-domains) data backfill + column prep.
    /// <para>
    /// Steps performed in order:
    /// </para>
    /// <list type="number">
    ///   <item>Lengthen <c>host_name</c> to <c>varchar(253)</c> to fit the public DNS label.</item>
    ///   <item>Add nullable <c>slug</c> + <c>is_active</c> columns.</item>
    ///   <item>
    ///     Backfill data idempotently:
    ///     <list type="bullet">
    ///       <item><c>slug</c> ← <c>slugify(dealer_name)</c> for rows where slug is null.</item>
    ///       <item><c>host_name</c> ← <c>slug || '.carstore.com'</c> for rows where host_name is null.</item>
    ///     </list>
    ///     On a collision the second writer is logged <c>RAISE NOTICE</c> and left untouched —
    ///     the next migration enforces uniqueness via partial UNIQUE indexes.
    ///   </item>
    /// </list>
    /// <para>
    /// NOT NULL + UNIQUE constraints live in the FOLLOWING migration
    /// (<c>AddDealerSettingsHostNameUniqueIndex</c>). That split is required by the
    /// design rollback plan so the backfill can complete before any constraint
    /// would reject legacy NULLs.
    /// </para>
    /// </summary>
    public partial class BackfillDealerSettingsHostName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Lengthen host_name to fit a fully-qualified hostname (≤253 chars).
            migrationBuilder.AlterColumn<string>(
                name: "host_name",
                schema: "public",
                table: "dealer_settings",
                type: "character varying(253)",
                maxLength: 253,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            // 2. Add new nullable columns. NOT NULL is enforced in the follow-up migration
            //    AFTER the backfill populates the data.
            migrationBuilder.AddColumn<string>(
                name: "slug",
                schema: "public",
                table: "dealer_settings",
                type: "character varying(63)",
                maxLength: 63,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                schema: "public",
                table: "dealer_settings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            // 3. Idempotent data backfill. Runs in a single transaction; the
            //    CONCURRENTLY keyword is intentionally NOT used here because
            //    PostgreSQL requires CONCURRENTLY-created indexes to run
            //    outside a transaction. The indexes themselves are added in
            //    AddDealerSettingsHostNameUniqueIndex so they CAN use
            //    CONCURRENTLY.
            //
            //    Slugify = lowercase, replace non-alnum with '-', trim leading/trailing
            //    hyphens, collapse runs of '-', cap at 63 chars. Implements the O5 RFC 1035
            //    validator used at the Domain layer.
            migrationBuilder.Sql(@"
                DO $$
                DECLARE
                    r record;
                    new_slug text;
                    new_host text;
                    truncated_count int := 0;
                    collision_slug_count int := 0;
                    collision_host_count int := 0;
                BEGIN
                    FOR r IN
                        SELECT id, dealer_id, dealer_name, slug, host_name
                        FROM public.dealer_settings
                    LOOP
                        -- Compute a deterministic slug from DealerName.
                        new_slug := lower(regexp_replace(
                            regexp_replace(
                                regexp_replace(coalesce(r.dealer_name, ''), '[^a-zA-Z0-9]+', '-', 'g'),
                                '^-+|-+$', '', 'g'),
                            '-{2,}', '-', 'g'));
                        -- RFC 1035 max length 63.
                        IF length(new_slug) > 63 THEN
                            new_slug := substring(new_slug, 1, 63);
                            -- Trim a trailing '-' if any (from a mid-cut).
                            new_slug := regexp_replace(new_slug, '-+$', '');
                            truncated_count := truncated_count + 1;
                        END IF;
                        -- Minimum viable slug; fallback to id if name was empty.
                        IF new_slug IS NULL OR new_slug = '' THEN
                            new_slug := 'dealer-' || substring(replace(r.dealer_id::text, '-', ''), 1, 12);
                        END IF;

                        -- Populate empty slug (skip on collision — log only).
                        IF r.slug IS NULL OR r.slug = '' THEN
                            IF EXISTS (
                                SELECT 1 FROM public.dealer_settings
                                WHERE slug = new_slug AND id <> r.id
                            ) THEN
                                RAISE NOTICE 'BackfillDealerSettingsHostName: slug collision for dealer_id=% on slug=% — leaving slug NULL (manual fixup required).',
                                    r.dealer_id, new_slug;
                                collision_slug_count := collision_slug_count + 1;
                            ELSE
                                UPDATE public.dealer_settings
                                SET slug = new_slug
                                WHERE id = r.id;
                            END IF;
                        END IF;

                        -- Populate empty host_name from slug. Skip collisions.
                        IF (r.host_name IS NULL OR r.host_name = '') AND r.slug IS NOT NULL THEN
                            new_host := r.slug || '.carstore.com';
                            IF EXISTS (
                                SELECT 1 FROM public.dealer_settings
                                WHERE host_name = new_host AND id <> r.id
                            ) THEN
                                RAISE NOTICE 'BackfillDealerSettingsHostName: host_name collision for dealer_id=% on host_name=% — leaving host_name NULL (manual fixup required).',
                                    r.dealer_id, new_host;
                                collision_host_count := collision_host_count + 1;
                            ELSE
                                UPDATE public.dealer_settings
                                SET host_name = new_host
                                WHERE id = r.id;
                            END IF;
                        END IF;
                    END LOOP;

                    RAISE NOTICE 'BackfillDealerSettingsHostName: backfill complete. truncated=% slug_collisions=% host_collisions=%',
                        truncated_count, collision_slug_count, collision_host_count;
                END
                $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_active",
                schema: "public",
                table: "dealer_settings");

            migrationBuilder.DropColumn(
                name: "slug",
                schema: "public",
                table: "dealer_settings");

            migrationBuilder.AlterColumn<string>(
                name: "host_name",
                schema: "public",
                table: "dealer_settings",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(253)",
                oldMaxLength: 253,
                oldNullable: true);
        }
    }
}

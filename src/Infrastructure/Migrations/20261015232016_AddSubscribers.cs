using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscribers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "subscribers",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    email_normalized = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    confirm_token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    confirm_token_expires_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    unsubscribe_token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    confirmed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    unsubscribed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    dealer_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_subscribers", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ux_subscribers_confirm_token_hash",
                schema: "public",
                table: "subscribers",
                column: "confirm_token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_subscribers_dealer_id_email_normalized",
                schema: "public",
                table: "subscribers",
                columns: new[] { "dealer_id", "email_normalized" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_subscribers_unsubscribe_token_hash",
                schema: "public",
                table: "subscribers",
                column: "unsubscribe_token_hash",
                unique: true);

            // sfc B5.1.4 backfill. The legacy sign-ups were placeholder clients (DNI 'NL-…', first name 'Newsletter').
            // 1) Keep a copy for Down(). 2) Each becomes a Confirmed 'legacy' subscriber: they were already receiving
            //    the newsletter. The unsubscribe digest is random because no link was ever emailed; the next send
            //    rotates it. 3) A placeholder nothing references is deleted; one that is referenced is soft-deleted.
            // Pre-check: scripts/migrate-newsletter-clients-dryrun.sql reports what this will do.
            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS public.newsletter_legacy_clients_backup AS
                    SELECT * FROM public.clients WITH NO DATA;

                INSERT INTO public.newsletter_legacy_clients_backup
                SELECT c.* FROM public.clients c
                WHERE c.dni LIKE 'NL-%' AND c.first_name = 'Newsletter'
                  AND NOT EXISTS (SELECT 1 FROM public.newsletter_legacy_clients_backup b WHERE b.id = c.id);

                INSERT INTO public.subscribers (id, email, email_normalized, status, source, confirm_token_hash,
                    confirm_token_expires_at_utc, unsubscribe_token_hash, created_at_utc, confirmed_at_utc,
                    unsubscribed_at_utc, dealer_id)
                SELECT DISTINCT ON (c.dealer_id, lower(trim(c.email)))
                    gen_random_uuid(), trim(c.email), lower(trim(c.email)), 'Confirmed', 'legacy', NULL, NULL,
                    upper(encode(sha256(convert_to(gen_random_uuid()::text || clock_timestamp()::text, 'UTF8')), 'hex')),
                    c.created_at, c.created_at, NULL, c.dealer_id
                FROM public.clients c
                WHERE c.dni LIKE 'NL-%' AND c.first_name = 'Newsletter' AND c.is_deleted = false AND coalesce(trim(c.email), '') <> ''
                ORDER BY c.dealer_id, lower(trim(c.email)), c.created_at
                ON CONFLICT (dealer_id, email_normalized) DO NOTHING;

                DELETE FROM public.clients c
                WHERE c.dni LIKE 'NL-%' AND c.first_name = 'Newsletter'
                  AND NOT EXISTS (SELECT 1 FROM public.quotes x WHERE x.client_id = c.id)
                  AND NOT EXISTS (SELECT 1 FROM public.sales x WHERE x.client_id = c.id)
                  AND NOT EXISTS (SELECT 1 FROM public.deposits x WHERE x.client_id = c.id)
                  AND NOT EXISTS (SELECT 1 FROM public.appointments x WHERE x.client_id = c.id)
                  AND NOT EXISTS (SELECT 1 FROM public.documents x WHERE x.client_id = c.id)
                  AND NOT EXISTS (SELECT 1 FROM public.transactions x WHERE x.client_id = c.id)
                  AND NOT EXISTS (SELECT 1 FROM public.warranty_records x WHERE x.client_id = c.id)
                  AND NOT EXISTS (SELECT 1 FROM public.service_reminders x WHERE x.client_id = c.id)
                  AND NOT EXISTS (SELECT 1 FROM public.leads x WHERE x.converted_client_id = c.id);

                UPDATE public.clients c SET is_deleted = true, deleted_at_utc = now()
                WHERE c.dni LIKE 'NL-%' AND c.first_name = 'Newsletter' AND c.is_deleted = false;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restores the legacy placeholder clients exactly as they were (deleted rows re-inserted, soft-deletes undone).
            migrationBuilder.Sql("""
                DO $$
                DECLARE
                    cols text;
                BEGIN
                    IF to_regclass('public.newsletter_legacy_clients_backup') IS NOT NULL THEN
                        UPDATE public.clients c SET is_deleted = b.is_deleted, deleted_at_utc = b.deleted_at_utc
                        FROM public.newsletter_legacy_clients_backup b WHERE c.id = b.id;

                        -- Generated columns (search_name) cannot be inserted: list the stored ones explicitly.
                        SELECT string_agg(quote_ident(column_name), ', ' ORDER BY ordinal_position) INTO cols
                        FROM information_schema.columns
                        WHERE table_schema = 'public' AND table_name = 'clients' AND is_generated = 'NEVER';

                        EXECUTE format(
                            'INSERT INTO public.clients (%1$s) SELECT %1$s FROM public.newsletter_legacy_clients_backup b
                             WHERE NOT EXISTS (SELECT 1 FROM public.clients c WHERE c.id = b.id)', cols);

                        DROP TABLE public.newsletter_legacy_clients_backup;
                    END IF;
                END $$;
                """);

            migrationBuilder.DropTable(
                name: "subscribers",
                schema: "public");
        }
    }
}

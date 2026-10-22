using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddQuoteNumbers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "number",
                schema: "public",
                table: "quotes",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "dealer_sequences",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    next_value = table.Column<long>(type: "bigint", nullable: false),
                    dealer_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dealer_sequences", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ux_quotes_dealer_id_number",
                schema: "public",
                table: "quotes",
                columns: new[] { "dealer_id", "number" },
                unique: true,
                filter: "number IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_dealer_sequences_dealer_id_kind",
                schema: "public",
                table: "dealer_sequences",
                columns: new[] { "dealer_id", "kind" },
                unique: true);

            // sfc B4.3 / RN-018 backfill. Numbers every pre-existing quote per dealer, oldest
            // first, using the same ROW_NUMBER() ordering pre-checked in staging via
            // scripts/check-quote-numbers-backfill-dryrun.sql, then seeds dealer_sequences from
            // the numbers actually written (not a recomputed count) so a partial/idempotent rerun
            // — WHERE number IS NULL only touches rows still missing one — still ends with a
            // correct next_value. Deleted quotes (is_deleted = true) are numbered too: they
            // already existed and a folio is immutable history, not a visibility concern.
            migrationBuilder.Sql("""
                WITH numbered AS (
                    SELECT id,
                           dealer_id,
                           ROW_NUMBER() OVER (PARTITION BY dealer_id ORDER BY created_at, id) AS seq
                    FROM public.quotes
                    WHERE number IS NULL
                )
                UPDATE public.quotes q
                SET number = 'COT-' || to_char(q.created_at, 'YYYY') || '-' || lpad(n.seq::text, 6, '0')
                FROM numbered n
                WHERE q.id = n.id;

                INSERT INTO public.dealer_sequences (id, dealer_id, kind, next_value)
                SELECT gen_random_uuid(), dealer_id, 'quote', max(right(number, 6)::bigint) + 1
                FROM public.quotes
                WHERE number IS NOT NULL
                GROUP BY dealer_id
                ON CONFLICT (dealer_id, kind) DO UPDATE SET next_value = excluded.next_value;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "dealer_sequences",
                schema: "public");

            migrationBuilder.DropIndex(
                name: "ux_quotes_dealer_id_number",
                schema: "public",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "number",
                schema: "public",
                table: "quotes");
        }
    }
}

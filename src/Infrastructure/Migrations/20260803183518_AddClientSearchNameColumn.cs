using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClientSearchNameColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // qa-p0-blockers C1 (D1 superseded, 2026-08-03): the scaffolded migration also picked
            // up two unrelated pending model changes (appointments.status, the
            // ux_sales_one_completed_per_car index) from other in-flight, not-yet-migrated work.
            // Those are out of scope for this C1-only batch and have been stripped so this
            // migration touches only the client search_name column/index it is named for.
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS unaccent;");
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_trgm;");

            // unaccent() is STABLE, not IMMUTABLE, so it cannot back a generated column or an
            // index directly. The two-argument form (explicit dictionary name) is required for
            // IMMUTABLE: the one-argument form does a runtime search_path/config lookup and is
            // therefore not index-safe.
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION public.f_unaccent(text)
                RETURNS text AS
                $$
                    SELECT public.unaccent('unaccent', $1)
                $$
                LANGUAGE sql IMMUTABLE PARALLEL SAFE STRICT;
            ");

            migrationBuilder.AddColumn<string>(
                name: "search_name",
                schema: "public",
                table: "clients",
                type: "text",
                nullable: true,
                computedColumnSql: "lower(f_unaccent(first_name || ' ' || last_name))",
                stored: true,
                collation: "C");

            migrationBuilder.CreateIndex(
                name: "ix_clients_search_name_trgm",
                schema: "public",
                table: "clients",
                column: "search_name")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_clients_search_name_trgm",
                schema: "public",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "search_name",
                schema: "public",
                table: "clients");

            migrationBuilder.Sql("DROP FUNCTION IF EXISTS public.f_unaccent(text);");

            // Extensions are left installed on Down — additive, and other features may already
            // depend on unaccent/pg_trgm existing. Dropping them here would be destructive far
            // beyond the scope of what this migration added.
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBranches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "branch_id",
                schema: "public",
                table: "cars",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "branches",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    name_normalized = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    is_main = table.Column<bool>(type: "boolean", nullable: false),
                    dealer_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_branches", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "user_branches",
                schema: "public",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_branches", x => new { x.user_id, x.branch_id });
                });

            migrationBuilder.CreateIndex(
                name: "ux_branches_dealer_id_name_normalized",
                schema: "public",
                table: "branches",
                columns: new[] { "dealer_id", "name_normalized" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_branches_branch_id",
                schema: "public",
                table: "user_branches",
                column: "branch_id");

            // sfc C2.1 / ADR C26 backfill: one "Casa central" per dealer holding every existing car and user, so
            // single-branch dealers keep working unchanged. Idempotent: safe to re-run. Pre-check in staging before
            // production (C2.1.3): no car or user should point to a dealer without dealer_settings.
            migrationBuilder.Sql("""
                INSERT INTO public.branches (id, dealer_id, name, name_normalized, is_main)
                SELECT gen_random_uuid(), ds.dealer_id, 'Casa central', 'CASA CENTRAL', TRUE
                FROM public.dealer_settings ds
                WHERE NOT EXISTS (SELECT 1 FROM public.branches b WHERE b.dealer_id = ds.dealer_id);

                UPDATE public.cars c
                SET branch_id = b.id
                FROM public.branches b
                WHERE b.dealer_id = c.dealer_id AND b.is_main AND c.branch_id IS NULL;

                INSERT INTO public.user_branches (user_id, branch_id)
                SELECT u.id, b.id
                FROM public.users u
                JOIN public.branches b ON b.dealer_id = u.dealer_id AND b.is_main
                ON CONFLICT DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "branches",
                schema: "public");

            migrationBuilder.DropTable(
                name: "user_branches",
                schema: "public");

            migrationBuilder.DropColumn(
                name: "branch_id",
                schema: "public",
                table: "cars");
        }
    }
}

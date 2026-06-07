using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBackfillAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // REQ-FVIP-1: append-only audit table for the admin backfill endpoint.
            // Created as a tenant-scoped table — every row carries a DealerId and the
            // ApplicationDbContext applies a Global Query Filter for tenancy isolation.
            // No FK to users on purpose: the audit log must survive user deletion.
            migrationBuilder.CreateTable(
                name: "backfill_audit",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    dealer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    affected_row_count = table.Column<int>(type: "integer", nullable: false),
                    execution_time_ms = table.Column<int>(type: "integer", nullable: true),
                    metadata_json = table.Column<string>(type: "text", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_backfill_audit", x => x.id);
                });

            // Dominant access pattern: list the most recent audits for a given dealer.
            migrationBuilder.CreateIndex(
                name: "ix_backfill_audit_dealer_created",
                schema: "public",
                table: "backfill_audit",
                columns: new[] { "dealer_id", "created_at_utc" });

            // Investigative access pattern: filter audit by actor user.
            migrationBuilder.CreateIndex(
                name: "ix_backfill_audit_actor",
                schema: "public",
                table: "backfill_audit",
                column: "actor_user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "backfill_audit",
                schema: "public");
        }
    }
}

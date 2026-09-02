using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPlatformAuditLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "platform_audit_logs",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    dealer_settings_id = table.Column<Guid>(type: "uuid", nullable: false),
                    dealer_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    action = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    actor_kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    occurred_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    recorded_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    source_event_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    dealer_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_platform_audit_logs", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_platform_audit_logs_action_occurred_at_utc",
                schema: "public",
                table: "platform_audit_logs",
                columns: new[] { "action", "occurred_at_utc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_platform_audit_logs_dealer_id_occurred_at_utc",
                schema: "public",
                table: "platform_audit_logs",
                columns: new[] { "dealer_id", "occurred_at_utc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_platform_audit_logs_occurred_at_utc",
                schema: "public",
                table: "platform_audit_logs",
                column: "occurred_at_utc",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "ux_platform_audit_logs_source_event_key",
                schema: "public",
                table: "platform_audit_logs",
                column: "source_event_key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "platform_audit_logs",
                schema: "public");
        }
    }
}

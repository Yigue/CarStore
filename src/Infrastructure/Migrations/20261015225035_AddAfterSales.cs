using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAfterSales : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "service_reminder_months",
                schema: "public",
                table: "dealer_settings",
                type: "integer",
                nullable: false,
                defaultValue: 6);

            migrationBuilder.AddColumn<bool>(
                name: "accepts_service_reminders",
                schema: "public",
                table: "clients",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "service_reminders",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sale_id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    due_on_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    client_contact_allowed = table.Column<bool>(type: "boolean", nullable: false),
                    dealer_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_service_reminders", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "warranty_records",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sale_id = table.Column<Guid>(type: "uuid", nullable: false),
                    car_id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_id = table.Column<Guid>(type: "uuid", nullable: false),
                    starts_on_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    months = table.Column<int>(type: "integer", nullable: false),
                    ends_on_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    conditions = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    dealer_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_warranty_records", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_service_reminders_dealer_due",
                schema: "public",
                table: "service_reminders",
                columns: new[] { "dealer_id", "due_on_utc" });

            migrationBuilder.CreateIndex(
                name: "ux_service_reminders_sale_kind_due",
                schema: "public",
                table: "service_reminders",
                columns: new[] { "sale_id", "kind", "due_on_utc" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_warranty_records_dealer_ends_on",
                schema: "public",
                table: "warranty_records",
                columns: new[] { "dealer_id", "ends_on_utc" });

            migrationBuilder.CreateIndex(
                name: "ux_warranty_records_sale_id",
                schema: "public",
                table: "warranty_records",
                column: "sale_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "service_reminders",
                schema: "public");

            migrationBuilder.DropTable(
                name: "warranty_records",
                schema: "public");

            migrationBuilder.DropColumn(
                name: "service_reminder_months",
                schema: "public",
                table: "dealer_settings");

            migrationBuilder.DropColumn(
                name: "accepts_service_reminders",
                schema: "public",
                table: "clients");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBillingRefundRecordsAndBillingEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "billing_email",
                schema: "public",
                table: "dealer_settings",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "billing_refund_records",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    subscription_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_event_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    charge_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    invoice_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    charge_amount_minor = table.Column<long>(type: "bigint", nullable: false),
                    amount_refunded_minor = table.Column<long>(type: "bigint", nullable: false),
                    currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    reason = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    kind = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    occurred_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    recorded_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    dealer_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_billing_refund_records", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_billing_refund_records_dealer_id_occurred_at_utc",
                schema: "public",
                table: "billing_refund_records",
                columns: new[] { "dealer_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ux_billing_refund_records_provider_event_id",
                schema: "public",
                table: "billing_refund_records",
                column: "provider_event_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "billing_refund_records",
                schema: "public");

            migrationBuilder.DropColumn(
                name: "billing_email",
                schema: "public",
                table: "dealer_settings");
        }
    }
}

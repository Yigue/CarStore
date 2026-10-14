using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDeposits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "deposits",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    car_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quote_id = table.Column<Guid>(type: "uuid", nullable: true),
                    client_id = table.Column<Guid>(type: "uuid", nullable: true),
                    lead_id = table.Column<Guid>(type: "uuid", nullable: true),
                    amount = table.Column<decimal>(type: "numeric", nullable: false),
                    amount_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    exchange_rate_base = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: true),
                    exchange_rate_quote = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: true),
                    exchange_rate = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    exchange_rate_captured_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    exchange_rate_source_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    refunded_amount = table.Column<decimal>(type: "numeric", nullable: true),
                    refunded_amount_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    refund_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    refunded_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    applied_sale_id = table.Column<Guid>(type: "uuid", nullable: true),
                    dealer_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_deposits", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_deposits_dealer_id",
                schema: "public",
                table: "deposits",
                column: "dealer_id");

            migrationBuilder.CreateIndex(
                name: "ix_deposits_status_expires_at_utc",
                schema: "public",
                table: "deposits",
                columns: new[] { "status", "expires_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ux_deposits_one_active_per_car",
                schema: "public",
                table: "deposits",
                column: "car_id",
                unique: true,
                filter: "status = 'Active'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "deposits",
                schema: "public");
        }
    }
}

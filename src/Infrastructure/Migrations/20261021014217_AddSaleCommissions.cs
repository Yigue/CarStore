using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSaleCommissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "commission_policies",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    default_commission_rate = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    overrides_json = table.Column<string>(type: "text", nullable: true),
                    dealer_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_commission_policies", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sale_commissions",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sale_id = table.Column<Guid>(type: "uuid", nullable: false),
                    salesperson_id = table.Column<Guid>(type: "uuid", nullable: true),
                    base_amount = table.Column<decimal>(type: "numeric", nullable: false),
                    base_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    rate = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    amount = table.Column<decimal>(type: "numeric", nullable: false),
                    amount_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    is_configured = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    calculated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    dealer_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sale_commissions", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ux_commission_policies_dealer_id",
                schema: "public",
                table: "commission_policies",
                column: "dealer_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sale_commissions_dealer_id",
                schema: "public",
                table: "sale_commissions",
                column: "dealer_id");

            migrationBuilder.CreateIndex(
                name: "ix_sale_commissions_dealer_id_salesperson_id",
                schema: "public",
                table: "sale_commissions",
                columns: new[] { "dealer_id", "salesperson_id" });

            migrationBuilder.CreateIndex(
                name: "ux_sale_commissions_sale_id",
                schema: "public",
                table: "sale_commissions",
                column: "sale_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "commission_policies",
                schema: "public");

            migrationBuilder.DropTable(
                name: "sale_commissions",
                schema: "public");
        }
    }
}

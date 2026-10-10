using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMoneyCurrencyColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ADR C1 (saas-functional-completeness B0.1): every Money gets a currency column next to
            // its amount. Backfill = 'ARS' because that is exactly what the API has always returned
            // (the old converter rebuilt every Money as ARS), so nothing observable changes.
            //
            // Safe on a live table: ADD COLUMN ... DEFAULT <constant> is a metadata-only change on
            // PostgreSQL 11+, so no rewrite and no long lock. The DEFAULT stays on purpose
            // (expand/contract): during the blue/green overlap the previous release still inserts
            // rows without these columns, and the default keeps those rows readable as ARS.
            // The optional columns also get the default; a currency on a NULL amount reads back as
            // "no amount" (covered by MoneyRoundTripTests).
            // Rollback: Down() drops the columns (amounts are untouched; only the currency is lost,
            // which is the pre-migration behavior).
            migrationBuilder.AddColumn<string>(
                name: "amount_currency",
                schema: "public",
                table: "transactions",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "ARS");

            migrationBuilder.AddColumn<string>(
                name: "down_payment_currency",
                schema: "public",
                table: "sales",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true,
                defaultValue: "ARS");

            migrationBuilder.AddColumn<string>(
                name: "final_price_currency",
                schema: "public",
                table: "sales",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "ARS");

            migrationBuilder.AddColumn<string>(
                name: "financed_amount_currency",
                schema: "public",
                table: "sales",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true,
                defaultValue: "ARS");

            migrationBuilder.AddColumn<string>(
                name: "installment_amount_currency",
                schema: "public",
                table: "sales",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true,
                defaultValue: "ARS");

            migrationBuilder.AddColumn<string>(
                name: "trade_in_value_currency",
                schema: "public",
                table: "sales",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true,
                defaultValue: "ARS");

            migrationBuilder.AddColumn<string>(
                name: "proposed_price_currency",
                schema: "public",
                table: "quotes",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "ARS");

            migrationBuilder.AddColumn<string>(
                name: "price_currency",
                schema: "public",
                table: "cars",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "ARS");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "amount_currency",
                schema: "public",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "down_payment_currency",
                schema: "public",
                table: "sales");

            migrationBuilder.DropColumn(
                name: "final_price_currency",
                schema: "public",
                table: "sales");

            migrationBuilder.DropColumn(
                name: "financed_amount_currency",
                schema: "public",
                table: "sales");

            migrationBuilder.DropColumn(
                name: "installment_amount_currency",
                schema: "public",
                table: "sales");

            migrationBuilder.DropColumn(
                name: "trade_in_value_currency",
                schema: "public",
                table: "sales");

            migrationBuilder.DropColumn(
                name: "proposed_price_currency",
                schema: "public",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "price_currency",
                schema: "public",
                table: "cars");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSalePaymentAndLegalDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "delivery_date",
                schema: "public",
                table: "sales",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "down_payment",
                schema: "public",
                table: "sales",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "financed_amount",
                schema: "public",
                table: "sales",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "financing_entity",
                schema: "public",
                table: "sales",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "installment_amount",
                schema: "public",
                table: "sales",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "installment_count",
                schema: "public",
                table: "sales",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "invoice_number",
                schema: "public",
                table: "sales",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "registration_number",
                schema: "public",
                table: "sales",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "trade_in_car_id",
                schema: "public",
                table: "sales",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "trade_in_value",
                schema: "public",
                table: "sales",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "transfer_form_number",
                schema: "public",
                table: "sales",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_sales_trade_in_car_id",
                schema: "public",
                table: "sales",
                column: "trade_in_car_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_sales_trade_in_car_id",
                schema: "public",
                table: "sales");

            migrationBuilder.DropColumn(
                name: "delivery_date",
                schema: "public",
                table: "sales");

            migrationBuilder.DropColumn(
                name: "down_payment",
                schema: "public",
                table: "sales");

            migrationBuilder.DropColumn(
                name: "financed_amount",
                schema: "public",
                table: "sales");

            migrationBuilder.DropColumn(
                name: "financing_entity",
                schema: "public",
                table: "sales");

            migrationBuilder.DropColumn(
                name: "installment_amount",
                schema: "public",
                table: "sales");

            migrationBuilder.DropColumn(
                name: "installment_count",
                schema: "public",
                table: "sales");

            migrationBuilder.DropColumn(
                name: "invoice_number",
                schema: "public",
                table: "sales");

            migrationBuilder.DropColumn(
                name: "registration_number",
                schema: "public",
                table: "sales");

            migrationBuilder.DropColumn(
                name: "trade_in_car_id",
                schema: "public",
                table: "sales");

            migrationBuilder.DropColumn(
                name: "trade_in_value",
                schema: "public",
                table: "sales");

            migrationBuilder.DropColumn(
                name: "transfer_form_number",
                schema: "public",
                table: "sales");
        }
    }
}

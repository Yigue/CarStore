using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSaleExchangeRateSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "exchange_rate",
                schema: "public",
                table: "sales",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "exchange_rate_base",
                schema: "public",
                table: "sales",
                type: "character(3)",
                fixedLength: true,
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "exchange_rate_captured_at_utc",
                schema: "public",
                table: "sales",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "exchange_rate_quote",
                schema: "public",
                table: "sales",
                type: "character(3)",
                fixedLength: true,
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "exchange_rate_source_id",
                schema: "public",
                table: "sales",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "exchange_rate",
                schema: "public",
                table: "sales");

            migrationBuilder.DropColumn(
                name: "exchange_rate_base",
                schema: "public",
                table: "sales");

            migrationBuilder.DropColumn(
                name: "exchange_rate_captured_at_utc",
                schema: "public",
                table: "sales");

            migrationBuilder.DropColumn(
                name: "exchange_rate_quote",
                schema: "public",
                table: "sales");

            migrationBuilder.DropColumn(
                name: "exchange_rate_source_id",
                schema: "public",
                table: "sales");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixModelMismatchV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "dealer_id",
                schema: "public",
                table: "transaction_categories");

            migrationBuilder.DropColumn(
                name: "dealer_id",
                schema: "public",
                table: "modelo");

            migrationBuilder.DropColumn(
                name: "dealer_id",
                schema: "public",
                table: "marca");

            migrationBuilder.DropColumn(
                name: "dealer_id",
                schema: "public",
                table: "car_images");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "dealer_id",
                schema: "public",
                table: "transaction_categories",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "dealer_id",
                schema: "public",
                table: "modelo",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "dealer_id",
                schema: "public",
                table: "marca",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "dealer_id",
                schema: "public",
                table: "car_images",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));
        }
    }
}

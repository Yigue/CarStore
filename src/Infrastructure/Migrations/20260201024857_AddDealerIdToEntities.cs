using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDealerIdToEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "dealer_id",
                schema: "public",
                table: "users",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "dealer_id",
                schema: "public",
                table: "UserPermissions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "dealer_id",
                schema: "public",
                table: "transactions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

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
                table: "sales",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "dealer_id",
                schema: "public",
                table: "quotes",
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
                table: "clients",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "dealer_id",
                schema: "public",
                table: "cars",
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "dealer_id",
                schema: "public",
                table: "users");

            migrationBuilder.DropColumn(
                name: "dealer_id",
                schema: "public",
                table: "UserPermissions");

            migrationBuilder.DropColumn(
                name: "dealer_id",
                schema: "public",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "dealer_id",
                schema: "public",
                table: "transaction_categories");

            migrationBuilder.DropColumn(
                name: "dealer_id",
                schema: "public",
                table: "sales");

            migrationBuilder.DropColumn(
                name: "dealer_id",
                schema: "public",
                table: "quotes");

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
                table: "clients");

            migrationBuilder.DropColumn(
                name: "dealer_id",
                schema: "public",
                table: "cars");

            migrationBuilder.DropColumn(
                name: "dealer_id",
                schema: "public",
                table: "car_images");
        }
    }
}

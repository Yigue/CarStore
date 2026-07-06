
#pragma warning disable IDE0161
#pragma warning disable IDE0053
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDealerSuspensionColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                schema: "public",
                table: "dealer_settings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "suspended_at",
                schema: "public",
                table: "dealer_settings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "suspend_reason",
                schema: "public",
                table: "dealer_settings",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_dealer_settings_is_active",
                schema: "public",
                table: "dealer_settings",
                column: "is_active");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_dealer_settings_is_active",
                schema: "public",
                table: "dealer_settings");

            migrationBuilder.DropColumn(
                name: "is_active",
                schema: "public",
                table: "dealer_settings");

            migrationBuilder.DropColumn(
                name: "suspended_at",
                schema: "public",
                table: "dealer_settings");

            migrationBuilder.DropColumn(
                name: "suspend_reason",
                schema: "public",
                table: "dealer_settings");
        }
    }
}

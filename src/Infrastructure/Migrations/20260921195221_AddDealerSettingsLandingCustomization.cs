using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDealerSettingsLandingCustomization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "carousel_slides_json",
                schema: "public",
                table: "dealer_settings",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "history_text",
                schema: "public",
                table: "dealer_settings",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "mission_text",
                schema: "public",
                table: "dealer_settings",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "values_text",
                schema: "public",
                table: "dealer_settings",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "vision_text",
                schema: "public",
                table: "dealer_settings",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "carousel_slides_json",
                schema: "public",
                table: "dealer_settings");

            migrationBuilder.DropColumn(
                name: "history_text",
                schema: "public",
                table: "dealer_settings");

            migrationBuilder.DropColumn(
                name: "mission_text",
                schema: "public",
                table: "dealer_settings");

            migrationBuilder.DropColumn(
                name: "values_text",
                schema: "public",
                table: "dealer_settings");

            migrationBuilder.DropColumn(
                name: "vision_text",
                schema: "public",
                table: "dealer_settings");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCarFeaturedAndTransmission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "featured",
                schema: "public",
                table: "cars",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "transmission",
                schema: "public",
                table: "cars",
                type: "text",
                nullable: false,
                defaultValue: "Manual");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "featured",
                schema: "public",
                table: "cars");

            migrationBuilder.DropColumn(
                name: "transmission",
                schema: "public",
                table: "cars");
        }
    }
}

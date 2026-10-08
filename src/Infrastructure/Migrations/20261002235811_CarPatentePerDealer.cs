using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CarPatentePerDealer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_cars_patente",
                schema: "public",
                table: "cars");

            migrationBuilder.CreateIndex(
                name: "ix_cars_dealer_patente",
                schema: "public",
                table: "cars",
                columns: new[] { "dealer_id", "patente" },
                unique: true,
                filter: "is_deleted = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_cars_dealer_patente",
                schema: "public",
                table: "cars");

            migrationBuilder.CreateIndex(
                name: "ix_cars_patente",
                schema: "public",
                table: "cars",
                column: "patente",
                unique: true);
        }
    }
}

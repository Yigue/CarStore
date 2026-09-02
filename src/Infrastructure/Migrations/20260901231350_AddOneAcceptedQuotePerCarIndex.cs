using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOneAcceptedQuotePerCarIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ux_quotes_one_accepted_per_car",
                schema: "public",
                table: "quotes",
                column: "car_id",
                unique: true,
                filter: "status = 'Accepted' AND is_deleted = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_quotes_one_accepted_per_car",
                schema: "public",
                table: "quotes");
        }
    }
}

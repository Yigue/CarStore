using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// qa-p1-integridad PR3, Slice 6 (D4, REQ: sale-completion-inventory-sync).
    /// Partial unique index restricting a car to at most one completed sale in the database.
    /// </summary>
    public partial class AddOneCompletedSalePerCarIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ux_sales_one_completed_per_car",
                schema: "public",
                table: "sales",
                column: "car_id",
                unique: true,
                filter: "status = 'Completed'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_sales_one_completed_per_car",
                schema: "public",
                table: "sales");
        }
    }
}

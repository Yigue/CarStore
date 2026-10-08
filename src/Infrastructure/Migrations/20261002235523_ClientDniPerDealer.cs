using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ClientDniPerDealer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_clients_dni",
                schema: "public",
                table: "clients");

            migrationBuilder.CreateIndex(
                name: "ix_clients_dealer_dni",
                schema: "public",
                table: "clients",
                columns: new[] { "dealer_id", "dni" },
                unique: true,
                filter: "is_deleted = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_clients_dealer_dni",
                schema: "public",
                table: "clients");

            migrationBuilder.CreateIndex(
                name: "ix_clients_dni",
                schema: "public",
                table: "clients",
                column: "dni",
                unique: true);
        }
    }
}

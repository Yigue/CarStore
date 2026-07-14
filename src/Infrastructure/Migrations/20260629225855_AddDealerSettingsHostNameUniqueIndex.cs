using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDealerSettingsHostNameUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_DealerSettings_HostName_Unique",
                schema: "public",
                table: "dealer_settings",
                column: "host_name",
                unique: true,
                filter: "host_name IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DealerSettings_HostName_Unique",
                schema: "public",
                table: "dealer_settings");
        }
    }
}

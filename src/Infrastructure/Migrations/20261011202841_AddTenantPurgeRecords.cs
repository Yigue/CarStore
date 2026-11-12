using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantPurgeRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tenant_purge_records",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    dealer_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    purged_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    counts_json = table.Column<string>(type: "text", nullable: false),
                    dealer_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenant_purge_records", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_tenant_purge_records_dealer_id",
                schema: "public",
                table: "tenant_purge_records",
                column: "dealer_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tenant_purge_records",
                schema: "public");
        }
    }
}

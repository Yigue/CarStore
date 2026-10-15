using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCarConsignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "consignment_commission_rate",
                schema: "public",
                table: "cars",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "consignment_owner_client_id",
                schema: "public",
                table: "cars",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "consignment_commission_rate",
                schema: "public",
                table: "cars");

            migrationBuilder.DropColumn(
                name: "consignment_owner_client_id",
                schema: "public",
                table: "cars");
        }
    }
}

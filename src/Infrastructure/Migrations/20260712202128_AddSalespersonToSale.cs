using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSalespersonToSale : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "salesperson_id",
                schema: "public",
                table: "sales",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_sales_salesperson_id",
                schema: "public",
                table: "sales",
                column: "salesperson_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_sales_salesperson_id",
                schema: "public",
                table: "sales");

            migrationBuilder.DropColumn(
                name: "salesperson_id",
                schema: "public",
                table: "sales");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TransactionCategoryTenancy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "dealer_id",
                schema: "public",
                table: "transaction_categories",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "ix_transaction_categories_dealer_id",
                schema: "public",
                table: "transaction_categories",
                column: "dealer_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_transaction_categories_dealer_id",
                schema: "public",
                table: "transaction_categories");

            migrationBuilder.DropColumn(
                name: "dealer_id",
                schema: "public",
                table: "transaction_categories");
        }
    }
}

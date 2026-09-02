using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentSaleLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "sale_id",
                schema: "public",
                table: "documents",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_documents_sale_id",
                schema: "public",
                table: "documents",
                column: "sale_id");

            migrationBuilder.AddForeignKey(
                name: "fk_documents_sales_sale_id",
                schema: "public",
                table: "documents",
                column: "sale_id",
                principalSchema: "public",
                principalTable: "sales",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_documents_sales_sale_id",
                schema: "public",
                table: "documents");

            migrationBuilder.DropIndex(
                name: "ix_documents_sale_id",
                schema: "public",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "sale_id",
                schema: "public",
                table: "documents");
        }
    }
}

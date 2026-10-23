using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentQuoteId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "quote_id",
                schema: "public",
                table: "documents",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_documents_quote_id",
                schema: "public",
                table: "documents",
                column: "quote_id");

            migrationBuilder.AddForeignKey(
                name: "fk_documents_quotes_quote_id",
                schema: "public",
                table: "documents",
                column: "quote_id",
                principalSchema: "public",
                principalTable: "quotes",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_documents_quotes_quote_id",
                schema: "public",
                table: "documents");

            migrationBuilder.DropIndex(
                name: "ix_documents_quote_id",
                schema: "public",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "quote_id",
                schema: "public",
                table: "documents");
        }
    }
}

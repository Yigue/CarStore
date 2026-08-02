using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// qa-p1-integridad PR2, Slice 4 (D3, REQ: finance-category-referential-integrity).
    /// <c>transactions.category_id</c> defaulted to EF's Cascade — the only one of four
    /// FinancialTransaction FKs not explicitly restricted — so deleting a referenced category
    /// destroyed every transaction that referenced it. Drops and re-adds the constraint with
    /// ON DELETE RESTRICT. Constrains future deletes only; validates nothing on existing rows,
    /// so there is no cleanup phase and Down() restores Cascade with no data movement.
    /// </summary>
    public partial class RestrictTransactionCategoryDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_transactions_transaction_categories_category_id",
                schema: "public",
                table: "transactions");

            migrationBuilder.AddForeignKey(
                name: "fk_transactions_transaction_categories_category_id",
                schema: "public",
                table: "transactions",
                column: "category_id",
                principalSchema: "public",
                principalTable: "transaction_categories",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_transactions_transaction_categories_category_id",
                schema: "public",
                table: "transactions");

            migrationBuilder.AddForeignKey(
                name: "fk_transactions_transaction_categories_category_id",
                schema: "public",
                table: "transactions",
                column: "category_id",
                principalSchema: "public",
                principalTable: "transaction_categories",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFinanceIdempotencyAndCompositeIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "reconditioning_task_id",
                schema: "public",
                table: "transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "source_id",
                schema: "public",
                table: "transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_transactions_DealerId_CategoryId",
                schema: "public",
                table: "transactions",
                columns: new[] { "dealer_id", "category_id" });

            migrationBuilder.CreateIndex(
                name: "IX_transactions_DealerId_TransactionDate",
                schema: "public",
                table: "transactions",
                columns: new[] { "dealer_id", "transaction_date" });

            migrationBuilder.CreateIndex(
                name: "IX_transactions_ReconditioningTaskId_SourceId",
                schema: "public",
                table: "transactions",
                columns: new[] { "reconditioning_task_id", "source_id" },
                unique: true,
                filter: "\"ReconditioningTaskId\" IS NOT NULL AND \"SourceId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_transactions_DealerId_CategoryId",
                schema: "public",
                table: "transactions");

            migrationBuilder.DropIndex(
                name: "IX_transactions_DealerId_TransactionDate",
                schema: "public",
                table: "transactions");

            migrationBuilder.DropIndex(
                name: "IX_transactions_ReconditioningTaskId_SourceId",
                schema: "public",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "reconditioning_task_id",
                schema: "public",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "source_id",
                schema: "public",
                table: "transactions");
        }
    }
}

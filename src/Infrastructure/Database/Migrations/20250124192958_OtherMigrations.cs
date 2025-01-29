#pragma warning disable IDE0161 // Convertir en namespace con ámbito de archivo
#pragma warning disable IDE0053
#pragma warning disable S4581
using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class OtherMigrations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_transactions_sales_sale_id1",
                schema: "public",
                table: "transactions");

            migrationBuilder.AlterColumn<Guid>(
                name: "sale_id1",
                schema: "public",
                table: "transactions",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddForeignKey(
                name: "fk_transactions_sales_sale_id1",
                schema: "public",
                table: "transactions",
                column: "sale_id1",
                principalSchema: "public",
                principalTable: "sales",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_transactions_sales_sale_id1",
                schema: "public",
                table: "transactions");

            migrationBuilder.AlterColumn<Guid>(
                name: "sale_id1",
                schema: "public",
                table: "transactions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid(),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "fk_transactions_sales_sale_id1",
                schema: "public",
                table: "transactions",
                column: "sale_id1",
                principalSchema: "public",
                principalTable: "sales",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}

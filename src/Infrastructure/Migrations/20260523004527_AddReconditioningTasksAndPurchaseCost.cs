using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReconditioningTasksAndPurchaseCost : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "purchase_cost_amount",
                schema: "public",
                table: "cars",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "purchase_cost_currency",
                schema: "public",
                table: "cars",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "reconditioning_tasks",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    car_id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    cost_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    cost_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    dealer_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reconditioning_tasks", x => x.id);
                    table.ForeignKey(
                        name: "fk_reconditioning_tasks_cars_car_id",
                        column: x => x.car_id,
                        principalSchema: "public",
                        principalTable: "cars",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_reconditioning_tasks_car_id",
                schema: "public",
                table: "reconditioning_tasks",
                column: "car_id");

            migrationBuilder.CreateIndex(
                name: "ix_reconditioning_tasks_dealer_id",
                schema: "public",
                table: "reconditioning_tasks",
                column: "dealer_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "reconditioning_tasks",
                schema: "public");

            migrationBuilder.DropColumn(
                name: "purchase_cost_amount",
                schema: "public",
                table: "cars");

            migrationBuilder.DropColumn(
                name: "purchase_cost_currency",
                schema: "public",
                table: "cars");
        }
    }
}

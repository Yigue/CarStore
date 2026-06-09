using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLeadToAppointment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "client_id",
                schema: "public",
                table: "quotes",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "lead_id",
                schema: "public",
                table: "quotes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "client_id",
                schema: "public",
                table: "appointments",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "lead_id",
                schema: "public",
                table: "appointments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_quotes_lead_id",
                schema: "public",
                table: "quotes",
                column: "lead_id");

            migrationBuilder.CreateIndex(
                name: "ix_appointments_lead_id",
                schema: "public",
                table: "appointments",
                column: "lead_id");

            migrationBuilder.AddForeignKey(
                name: "fk_appointments_leads_lead_id",
                schema: "public",
                table: "appointments",
                column: "lead_id",
                principalSchema: "public",
                principalTable: "leads",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_quotes_leads_lead_id",
                schema: "public",
                table: "quotes",
                column: "lead_id",
                principalSchema: "public",
                principalTable: "leads",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_appointments_leads_lead_id",
                schema: "public",
                table: "appointments");

            migrationBuilder.DropForeignKey(
                name: "fk_quotes_leads_lead_id",
                schema: "public",
                table: "quotes");

            migrationBuilder.DropIndex(
                name: "ix_quotes_lead_id",
                schema: "public",
                table: "quotes");

            migrationBuilder.DropIndex(
                name: "ix_appointments_lead_id",
                schema: "public",
                table: "appointments");

            migrationBuilder.DropColumn(
                name: "lead_id",
                schema: "public",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "lead_id",
                schema: "public",
                table: "appointments");

            migrationBuilder.AlterColumn<Guid>(
                name: "client_id",
                schema: "public",
                table: "quotes",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "client_id",
                schema: "public",
                table: "appointments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}

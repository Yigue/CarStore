using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCrmIntegrationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_quotes_leads_lead_id",
                schema: "public",
                table: "quotes");

            migrationBuilder.AddColumn<Guid>(
                name: "lead_id",
                schema: "public",
                table: "sales",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "quote_id",
                schema: "public",
                table: "sales",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "converted_client_id",
                schema: "public",
                table: "leads",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "interested_vehicle_id",
                schema: "public",
                table: "leads",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "loss_reason",
                schema: "public",
                table: "leads",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "origin_lead_id",
                schema: "public",
                table: "clients",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "type",
                schema: "public",
                table: "clients",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Individual");

            migrationBuilder.CreateIndex(
                name: "ix_sales_lead_id",
                schema: "public",
                table: "sales",
                column: "lead_id");

            migrationBuilder.CreateIndex(
                name: "ix_sales_quote_id",
                schema: "public",
                table: "sales",
                column: "quote_id");

            migrationBuilder.CreateIndex(
                name: "ix_leads_converted_client_id",
                schema: "public",
                table: "leads",
                column: "converted_client_id");

            migrationBuilder.CreateIndex(
                name: "ix_leads_interested_vehicle_id",
                schema: "public",
                table: "leads",
                column: "interested_vehicle_id");

            migrationBuilder.CreateIndex(
                name: "ix_clients_origin_lead_id",
                schema: "public",
                table: "clients",
                column: "origin_lead_id");

            migrationBuilder.AddForeignKey(
                name: "fk_clients_leads_origin_lead_id",
                schema: "public",
                table: "clients",
                column: "origin_lead_id",
                principalSchema: "public",
                principalTable: "leads",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_leads_cars_interested_vehicle_id",
                schema: "public",
                table: "leads",
                column: "interested_vehicle_id",
                principalSchema: "public",
                principalTable: "cars",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_leads_clients_converted_client_id",
                schema: "public",
                table: "leads",
                column: "converted_client_id",
                principalSchema: "public",
                principalTable: "clients",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_quotes_leads_lead_id",
                schema: "public",
                table: "quotes",
                column: "lead_id",
                principalSchema: "public",
                principalTable: "leads",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_sales_leads_lead_id",
                schema: "public",
                table: "sales",
                column: "lead_id",
                principalSchema: "public",
                principalTable: "leads",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_sales_quotes_quote_id",
                schema: "public",
                table: "sales",
                column: "quote_id",
                principalSchema: "public",
                principalTable: "quotes",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_clients_leads_origin_lead_id",
                schema: "public",
                table: "clients");

            migrationBuilder.DropForeignKey(
                name: "fk_leads_cars_interested_vehicle_id",
                schema: "public",
                table: "leads");

            migrationBuilder.DropForeignKey(
                name: "fk_leads_clients_converted_client_id",
                schema: "public",
                table: "leads");

            migrationBuilder.DropForeignKey(
                name: "fk_quotes_leads_lead_id",
                schema: "public",
                table: "quotes");

            migrationBuilder.DropForeignKey(
                name: "fk_sales_leads_lead_id",
                schema: "public",
                table: "sales");

            migrationBuilder.DropForeignKey(
                name: "fk_sales_quotes_quote_id",
                schema: "public",
                table: "sales");

            migrationBuilder.DropIndex(
                name: "ix_sales_lead_id",
                schema: "public",
                table: "sales");

            migrationBuilder.DropIndex(
                name: "ix_sales_quote_id",
                schema: "public",
                table: "sales");

            migrationBuilder.DropIndex(
                name: "ix_leads_converted_client_id",
                schema: "public",
                table: "leads");

            migrationBuilder.DropIndex(
                name: "ix_leads_interested_vehicle_id",
                schema: "public",
                table: "leads");

            migrationBuilder.DropIndex(
                name: "ix_clients_origin_lead_id",
                schema: "public",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "lead_id",
                schema: "public",
                table: "sales");

            migrationBuilder.DropColumn(
                name: "quote_id",
                schema: "public",
                table: "sales");

            migrationBuilder.DropColumn(
                name: "converted_client_id",
                schema: "public",
                table: "leads");

            migrationBuilder.DropColumn(
                name: "interested_vehicle_id",
                schema: "public",
                table: "leads");

            migrationBuilder.DropColumn(
                name: "loss_reason",
                schema: "public",
                table: "leads");

            migrationBuilder.DropColumn(
                name: "origin_lead_id",
                schema: "public",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "type",
                schema: "public",
                table: "clients");

            migrationBuilder.AddForeignKey(
                name: "fk_quotes_leads_lead_id",
                schema: "public",
                table: "quotes",
                column: "lead_id",
                principalSchema: "public",
                principalTable: "leads",
                principalColumn: "id");
        }
    }
}

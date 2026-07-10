using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClientSoftDeleteAndOutboxColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "aggregate_id",
                schema: "public",
                table: "OutboxMessages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "aggregate_type",
                schema: "public",
                table: "OutboxMessages",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "dealer_id",
                schema: "public",
                table: "OutboxMessages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "notes",
                schema: "public",
                table: "clients",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "acquisition_source",
                schema: "public",
                table: "clients",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "assigned_agent_id",
                schema: "public",
                table: "clients",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_at_utc",
                schema: "public",
                table: "clients",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "deleted_by",
                schema: "public",
                table: "clients",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                schema: "public",
                table: "clients",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "ix_outbox_dealer",
                schema: "public",
                table: "OutboxMessages",
                columns: new[] { "aggregate_type", "dealer_id" });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_type_occurred",
                schema: "public",
                table: "OutboxMessages",
                columns: new[] { "type", "occurred_on_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_clients_is_deleted",
                schema: "public",
                table: "clients",
                column: "is_deleted");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_outbox_dealer",
                schema: "public",
                table: "OutboxMessages");

            migrationBuilder.DropIndex(
                name: "ix_outbox_type_occurred",
                schema: "public",
                table: "OutboxMessages");

            migrationBuilder.DropIndex(
                name: "ix_clients_is_deleted",
                schema: "public",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "aggregate_id",
                schema: "public",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "aggregate_type",
                schema: "public",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "dealer_id",
                schema: "public",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "acquisition_source",
                schema: "public",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "assigned_agent_id",
                schema: "public",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "deleted_at_utc",
                schema: "public",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "deleted_by",
                schema: "public",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                schema: "public",
                table: "clients");

            migrationBuilder.AlterColumn<string>(
                name: "notes",
                schema: "public",
                table: "clients",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000,
                oldNullable: true);
        }
    }
}

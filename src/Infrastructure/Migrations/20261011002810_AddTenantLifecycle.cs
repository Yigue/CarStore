using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "cancelled_at",
                schema: "public",
                table: "dealer_settings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "lifecycle_status",
                schema: "public",
                table: "dealer_settings",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Active");

            migrationBuilder.AddColumn<DateTime>(
                name: "purge_scheduled_at",
                schema: "public",
                table: "dealer_settings",
                type: "timestamp with time zone",
                nullable: true);

            // Backfill: every dealer stays Active (default). Dealers whose subscription is already Cancelled
            // get their real CancelledAt, but NO purge schedule: purging stays a manual, reviewed decision.
            migrationBuilder.Sql(@"
UPDATE public.dealer_settings AS ds
SET cancelled_at = sub.cancelled_at
FROM public.dealer_subscriptions AS sub
WHERE sub.dealer_id = ds.dealer_id
  AND sub.status = 'Cancelled'
  AND sub.cancelled_at IS NOT NULL;");

            migrationBuilder.CreateIndex(
                name: "ix_dealer_settings_purge_scheduled_at",
                schema: "public",
                table: "dealer_settings",
                column: "purge_scheduled_at",
                filter: "purge_scheduled_at IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_dealer_settings_purge_scheduled_at",
                schema: "public",
                table: "dealer_settings");

            migrationBuilder.DropColumn(
                name: "cancelled_at",
                schema: "public",
                table: "dealer_settings");

            migrationBuilder.DropColumn(
                name: "lifecycle_status",
                schema: "public",
                table: "dealer_settings");

            migrationBuilder.DropColumn(
                name: "purge_scheduled_at",
                schema: "public",
                table: "dealer_settings");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDealerSubscriptionAndProcessedStripeEvent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                schema: "public",
                table: "dealer_settings",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                schema: "public",
                table: "dealer_settings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "suspend_reason",
                schema: "public",
                table: "dealer_settings",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "suspended_at",
                schema: "public",
                table: "dealer_settings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "public",
                table: "dealer_settings",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.CreateTable(
                name: "dealer_subscriptions",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    stripe_customer_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    stripe_subscription_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    trial_ends_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    current_period_start = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    current_period_end = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    cancelled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    plan_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    dealer_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dealer_subscriptions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "processed_stripe_events",
                schema: "public",
                columns: table => new
                {
                    stripe_event_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    processed_on_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    dealer_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_processed_stripe_events", x => x.stripe_event_id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_dealer_settings_is_active",
                schema: "public",
                table: "dealer_settings",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_dealer_subscriptions_status",
                schema: "public",
                table: "dealer_subscriptions",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_dealer_subscriptions_stripe_subscription_id",
                schema: "public",
                table: "dealer_subscriptions",
                column: "stripe_subscription_id");

            migrationBuilder.CreateIndex(
                name: "ux_dealer_subscriptions_dealer_id",
                schema: "public",
                table: "dealer_subscriptions",
                column: "dealer_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "dealer_subscriptions",
                schema: "public");

            migrationBuilder.DropTable(
                name: "processed_stripe_events",
                schema: "public");

            migrationBuilder.DropIndex(
                name: "ix_dealer_settings_is_active",
                schema: "public",
                table: "dealer_settings");

            migrationBuilder.DropColumn(
                name: "created_at",
                schema: "public",
                table: "dealer_settings");

            migrationBuilder.DropColumn(
                name: "is_active",
                schema: "public",
                table: "dealer_settings");

            migrationBuilder.DropColumn(
                name: "suspend_reason",
                schema: "public",
                table: "dealer_settings");

            migrationBuilder.DropColumn(
                name: "suspended_at",
                schema: "public",
                table: "dealer_settings");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "public",
                table: "dealer_settings");
        }
    }
}

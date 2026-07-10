using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <summary>
    /// dealer_settings.is_active is intentionally NOT added here — the
    /// saas-custom-domains PR1 migration (BackfillDealerSettingsHostName,
    /// 20260629231919) already adds it with the identical
    /// boolean/NOT NULL/default-true shape. This migration only adds the
    /// columns unique to the dealer-suspension feature (created_at,
    /// suspended_at, suspend_reason) plus the is_active lookup index.
    /// </summary>
    public partial class AddDealerSuspensionAndCreatedAtColumns : Migration
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
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<DateTime>(
                name: "suspended_at",
                schema: "public",
                table: "dealer_settings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "suspend_reason",
                schema: "public",
                table: "dealer_settings",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_dealer_settings_is_active",
                schema: "public",
                table: "dealer_settings",
                column: "is_active");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_dealer_settings_is_active",
                schema: "public",
                table: "dealer_settings");

            migrationBuilder.DropColumn(
                name: "suspended_at",
                schema: "public",
                table: "dealer_settings");

            migrationBuilder.DropColumn(
                name: "suspend_reason",
                schema: "public",
                table: "dealer_settings");

            migrationBuilder.DropColumn(
                name: "created_at",
                schema: "public",
                table: "dealer_settings");
        }
    }
}

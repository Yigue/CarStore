using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLeadAttribution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "attribution_fbclid",
                schema: "public",
                table: "leads",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "attribution_first_touch_at_utc",
                schema: "public",
                table: "leads",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "attribution_gclid",
                schema: "public",
                table: "leads",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "attribution_landing_path",
                schema: "public",
                table: "leads",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "attribution_referrer",
                schema: "public",
                table: "leads",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "attribution_utm_campaign",
                schema: "public",
                table: "leads",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "attribution_utm_content",
                schema: "public",
                table: "leads",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "attribution_utm_medium",
                schema: "public",
                table: "leads",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "attribution_utm_source",
                schema: "public",
                table: "leads",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "attribution_utm_term",
                schema: "public",
                table: "leads",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "attribution_fbclid",
                schema: "public",
                table: "leads");

            migrationBuilder.DropColumn(
                name: "attribution_first_touch_at_utc",
                schema: "public",
                table: "leads");

            migrationBuilder.DropColumn(
                name: "attribution_gclid",
                schema: "public",
                table: "leads");

            migrationBuilder.DropColumn(
                name: "attribution_landing_path",
                schema: "public",
                table: "leads");

            migrationBuilder.DropColumn(
                name: "attribution_referrer",
                schema: "public",
                table: "leads");

            migrationBuilder.DropColumn(
                name: "attribution_utm_campaign",
                schema: "public",
                table: "leads");

            migrationBuilder.DropColumn(
                name: "attribution_utm_content",
                schema: "public",
                table: "leads");

            migrationBuilder.DropColumn(
                name: "attribution_utm_medium",
                schema: "public",
                table: "leads");

            migrationBuilder.DropColumn(
                name: "attribution_utm_source",
                schema: "public",
                table: "leads");

            migrationBuilder.DropColumn(
                name: "attribution_utm_term",
                schema: "public",
                table: "leads");
        }
    }
}

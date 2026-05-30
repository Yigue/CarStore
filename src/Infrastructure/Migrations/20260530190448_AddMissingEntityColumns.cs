using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMissingEntityColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_user_permissions_user_id",
                schema: "public",
                table: "UserPermissions");

            migrationBuilder.AlterColumn<string>(
                name: "role",
                schema: "public",
                table: "users",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldDefaultValue: "Cliente");

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                schema: "public",
                table: "users",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                schema: "public",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "phone",
                schema: "public",
                table: "users",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "granted_at",
                schema: "public",
                table: "UserPermissions",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<Guid>(
                name: "granted_by",
                schema: "public",
                table: "UserPermissions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "footer_text",
                schema: "public",
                table: "dealer_settings",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "logo_url",
                schema: "public",
                table: "dealer_settings",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "primary_color",
                schema: "public",
                table: "dealer_settings",
                type: "character varying(7)",
                maxLength: 7,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "secondary_color",
                schema: "public",
                table: "dealer_settings",
                type: "character varying(7)",
                maxLength: 7,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_permissions_user_id_permission",
                schema: "public",
                table: "UserPermissions",
                columns: new[] { "user_id", "permission" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_user_permissions_user_id_permission",
                schema: "public",
                table: "UserPermissions");

            migrationBuilder.DropColumn(
                name: "created_at",
                schema: "public",
                table: "users");

            migrationBuilder.DropColumn(
                name: "is_active",
                schema: "public",
                table: "users");

            migrationBuilder.DropColumn(
                name: "phone",
                schema: "public",
                table: "users");

            migrationBuilder.DropColumn(
                name: "granted_at",
                schema: "public",
                table: "UserPermissions");

            migrationBuilder.DropColumn(
                name: "granted_by",
                schema: "public",
                table: "UserPermissions");

            migrationBuilder.DropColumn(
                name: "footer_text",
                schema: "public",
                table: "dealer_settings");

            migrationBuilder.DropColumn(
                name: "logo_url",
                schema: "public",
                table: "dealer_settings");

            migrationBuilder.DropColumn(
                name: "primary_color",
                schema: "public",
                table: "dealer_settings");

            migrationBuilder.DropColumn(
                name: "secondary_color",
                schema: "public",
                table: "dealer_settings");

            migrationBuilder.AlterColumn<string>(
                name: "role",
                schema: "public",
                table: "users",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Cliente",
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.CreateIndex(
                name: "ix_user_permissions_user_id",
                schema: "public",
                table: "UserPermissions",
                column: "user_id");
        }
    }
}

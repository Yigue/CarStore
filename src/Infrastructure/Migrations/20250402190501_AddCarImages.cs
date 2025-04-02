using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddCarImages : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "fk_car_image_cars_car_id",
            schema: "public",
            table: "car_image");

        migrationBuilder.DropPrimaryKey(
            name: "pk_car_image",
            schema: "public",
            table: "car_image");

        migrationBuilder.RenameTable(
            name: "car_image",
            schema: "public",
            newName: "car_images",
            newSchema: "public");

        migrationBuilder.RenameIndex(
            name: "ix_car_image_car_id",
            schema: "public",
            table: "car_images",
            newName: "ix_car_images_car_id");

        migrationBuilder.AlterColumn<int>(
            name: "order",
            schema: "public",
            table: "car_images",
            type: "integer",
            nullable: false,
            defaultValue: 0,
            oldClrType: typeof(int),
            oldType: "integer");

        migrationBuilder.AlterColumn<bool>(
            name: "is_primary",
            schema: "public",
            table: "car_images",
            type: "boolean",
            nullable: false,
            defaultValue: false,
            oldClrType: typeof(bool),
            oldType: "boolean");

        migrationBuilder.AlterColumn<string>(
            name: "image_url",
            schema: "public",
            table: "car_images",
            type: "character varying(500)",
            maxLength: 500,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "text");

        migrationBuilder.AddPrimaryKey(
            name: "pk_car_images",
            schema: "public",
            table: "car_images",
            column: "id");

        migrationBuilder.AddForeignKey(
            name: "fk_car_images_cars_car_id",
            schema: "public",
            table: "car_images",
            column: "car_id",
            principalSchema: "public",
            principalTable: "cars",
            principalColumn: "id",
            onDelete: ReferentialAction.Cascade);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "fk_car_images_cars_car_id",
            schema: "public",
            table: "car_images");

        migrationBuilder.DropPrimaryKey(
            name: "pk_car_images",
            schema: "public",
            table: "car_images");

        migrationBuilder.RenameTable(
            name: "car_images",
            schema: "public",
            newName: "car_image",
            newSchema: "public");

        migrationBuilder.RenameIndex(
            name: "ix_car_images_car_id",
            schema: "public",
            table: "car_image",
            newName: "ix_car_image_car_id");

        migrationBuilder.AlterColumn<int>(
            name: "order",
            schema: "public",
            table: "car_image",
            type: "integer",
            nullable: false,
            oldClrType: typeof(int),
            oldType: "integer",
            oldDefaultValue: 0);

        migrationBuilder.AlterColumn<bool>(
            name: "is_primary",
            schema: "public",
            table: "car_image",
            type: "boolean",
            nullable: false,
            oldClrType: typeof(bool),
            oldType: "boolean",
            oldDefaultValue: false);

        migrationBuilder.AlterColumn<string>(
            name: "image_url",
            schema: "public",
            table: "car_image",
            type: "text",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(500)",
            oldMaxLength: 500);

        migrationBuilder.AddPrimaryKey(
            name: "pk_car_image",
            schema: "public",
            table: "car_image",
            column: "id");

        migrationBuilder.AddForeignKey(
            name: "fk_car_image_cars_car_id",
            schema: "public",
            table: "car_image",
            column: "car_id",
            principalSchema: "public",
            principalTable: "cars",
            principalColumn: "id",
            onDelete: ReferentialAction.Cascade);
    }
}

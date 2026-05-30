using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RefactorCarImagesForObjectStorage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ADR-2 dual-mode: only ADDITIVE changes. ImageUrl is kept (made nullable) so legacy
            // images remain accessible; new MinIO images use ObjectKey.

            migrationBuilder.AlterColumn<string>(
                name: "image_url",
                schema: "public",
                table: "car_images",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500);

            migrationBuilder.AddColumn<string>(
                name: "object_key",
                schema: "public",
                table: "car_images",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "content_type",
                schema: "public",
                table: "car_images",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "size_bytes",
                schema: "public",
                table: "car_images",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_cover",
                schema: "public",
                table: "car_images",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "display_order",
                schema: "public",
                table: "car_images",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Backfill the new columns from the legacy ones so existing data keeps its meaning.
            migrationBuilder.Sql(
                "UPDATE public.car_images SET is_cover = is_primary, display_order = \"order\";");

            // Drop the legacy columns that were renamed into is_cover / display_order.
            migrationBuilder.DropColumn(
                name: "is_primary",
                schema: "public",
                table: "car_images");

            migrationBuilder.DropColumn(
                name: "order",
                schema: "public",
                table: "car_images");

            migrationBuilder.CreateIndex(
                name: "ix_car_images_car_id_display_order",
                schema: "public",
                table: "car_images",
                columns: new[] { "car_id", "display_order" });

            // REQ-VMS-7: at most one cover image per car (partial unique index, Postgres).
            migrationBuilder.CreateIndex(
                name: "ux_car_images_car_id_is_cover",
                schema: "public",
                table: "car_images",
                column: "car_id",
                unique: true,
                filter: "is_cover = true");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_car_images_car_id_is_cover",
                schema: "public",
                table: "car_images");

            migrationBuilder.DropIndex(
                name: "ix_car_images_car_id_display_order",
                schema: "public",
                table: "car_images");

            migrationBuilder.AddColumn<bool>(
                name: "is_primary",
                schema: "public",
                table: "car_images",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "order",
                schema: "public",
                table: "car_images",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(
                "UPDATE public.car_images SET is_primary = is_cover, \"order\" = display_order;");

            migrationBuilder.DropColumn(name: "object_key", schema: "public", table: "car_images");
            migrationBuilder.DropColumn(name: "content_type", schema: "public", table: "car_images");
            migrationBuilder.DropColumn(name: "size_bytes", schema: "public", table: "car_images");
            migrationBuilder.DropColumn(name: "is_cover", schema: "public", table: "car_images");
            migrationBuilder.DropColumn(name: "display_order", schema: "public", table: "car_images");

            migrationBuilder.AlterColumn<string>(
                name: "image_url",
                schema: "public",
                table: "car_images",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);
        }
    }
}

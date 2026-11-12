using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMarcaNombreNormalizado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Pre-check: fail migration early if duplicates with different casing exist
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM public.marca
                        GROUP BY upper(trim(nombre))
                        HAVING count(*) > 1
                    ) THEN
                        RAISE EXCEPTION 'Cannot apply migration AddMarcaNombreNormalizado: duplicate brands exist with different casing. Run scripts/dedupe-marcas.sql first.';
                    END IF;
                END $$;
                """);

            migrationBuilder.AddColumn<string>(
                name: "nombre_normalizado",
                schema: "public",
                table: "marca",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            // Backfill existing rows before enforcing the unique constraint
            migrationBuilder.Sql("""
                UPDATE public.marca
                SET nombre_normalizado = upper(trim(nombre));
                """);

            migrationBuilder.CreateIndex(
                name: "ix_marca_nombre_normalizado",
                schema: "public",
                table: "marca",
                column: "nombre_normalizado",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_marca_nombre_normalizado",
                schema: "public",
                table: "marca");

            migrationBuilder.DropColumn(
                name: "nombre_normalizado",
                schema: "public",
                table: "marca");
        }
    }
}

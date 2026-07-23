using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRbacFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {

            migrationBuilder.AddColumn<Guid>(
                name: "role_id",
                schema: "public",
                table: "users",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "roles",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    dealer_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_roles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "role_permissions",
                schema: "public",
                columns: table => new
                {
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    permission = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_role_permissions", x => new { x.role_id, x.permission });
                    table.ForeignKey(
                        name: "fk_role_permissions_roles_role_id",
                        column: x => x.role_id,
                        principalSchema: "public",
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql("DO $$\n" +
                "DECLARE\n" +
                "    tenant_record RECORD;\n" +
                "    admin_role_id uuid;\n" +
                "BEGIN\n" +
                "    FOR tenant_record IN SELECT DISTINCT dealer_id FROM public.users\n" +
                "    LOOP\n" +
                "        admin_role_id := gen_random_uuid();\n" +
                "        \n" +
                "        INSERT INTO public.roles (id, name, description, dealer_id)\n" +
                "        VALUES (admin_role_id, 'Admin', 'Migrated Default Admin Role', tenant_record.dealer_id);\n" +
                "        \n" +
                "        INSERT INTO public.role_permissions (role_id, permission)\n" +
                "        SELECT DISTINCT admin_role_id, permission\n" +
                "        FROM public.\"UserPermissions\" up\n" +
                "        JOIN public.users u ON up.user_id = u.id\n" +
                "        WHERE u.dealer_id = tenant_record.dealer_id ON CONFLICT DO NOTHING;\n" +
                "        \n" +
                "        UPDATE public.users\n" +
                "        SET role_id = admin_role_id\n" +
                "        WHERE dealer_id = tenant_record.dealer_id;\n" +
                "    END LOOP;\n" +
                "END $$;");

            migrationBuilder.DropColumn(
                name: "role",
                schema: "public",
                table: "users");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "role_permissions",
                schema: "public");

            migrationBuilder.DropTable(
                name: "roles",
                schema: "public");

            migrationBuilder.DropColumn(
                name: "role_id",
                schema: "public",
                table: "users");

            migrationBuilder.AddColumn<string>(
                name: "role",
                schema: "public",
                table: "users",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");
        }
    }
}

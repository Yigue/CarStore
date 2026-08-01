using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260801003752_GrantDocumentPermissionsToAdminRoles")]
    public partial class GrantDocumentPermissionsToAdminRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ON CONFLICT DO NOTHING relies on role_permissions' composite PK (role_id,
            // permission) — re-running this migration (or applying it against a dealer that
            // already has the grant from a fresh UsersSeeder run) inserts zero duplicate rows.
            migrationBuilder.Sql(
                "INSERT INTO public.role_permissions (role_id, permission)\n" +
                "SELECT r.id, p.permission\n" +
                "FROM public.roles r\n" +
                "CROSS JOIN (VALUES ('documents:read'), ('documents:create')) AS p(permission)\n" +
                "WHERE r.name = 'Admin'\n" +
                "ON CONFLICT DO NOTHING;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Additive-only rollback: delete exactly the two granted rows, no other data touched.
            migrationBuilder.Sql(
                "DELETE FROM public.role_permissions rp\n" +
                "USING public.roles r\n" +
                "WHERE rp.role_id = r.id\n" +
                "  AND r.name = 'Admin'\n" +
                "  AND rp.permission IN ('documents:read', 'documents:create');");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DropLegacyCRMPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Remove legacy permission strings that were aliased to clients:write in PR1.
            // Any user that had clients:create or clients:update should already have
            // clients:write (added by UsersSeeder reconciler). This migration drops the old rows.
            migrationBuilder.Sql(
                "DELETE FROM public.\"UserPermissions\" WHERE permission IN ('clients:create', 'clients:update');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Re-insert legacy strings for admin and empleado users (best-effort; seeder
            // will reconcile on next startup anyway).
            migrationBuilder.Sql(@"
                INSERT INTO public.""UserPermissions"" (id, user_id, permission, dealer_id)
                SELECT gen_random_uuid(), u.id, perm.permission, u.dealer_id
                FROM public.users u
                CROSS JOIN (VALUES ('clients:create'), ('clients:update')) AS perm(permission)
                WHERE u.role = 'Admin'
                  AND NOT EXISTS (
                      SELECT 1 FROM public.""UserPermissions"" up2
                      WHERE up2.user_id = u.id AND up2.permission = perm.permission
                  );");
        }
    }
}

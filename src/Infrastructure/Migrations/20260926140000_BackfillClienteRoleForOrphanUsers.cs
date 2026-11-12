using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <summary>
    /// PR 1.1 / D1 (EXP-001/API-001/CU-1): Data-only migration.
    /// Backfills the 'Cliente' role for existing tenant users who were registered with RoleId = Guid.Empty.
    /// Prevents legitimate registered clients from losing access due to the fail-closed login fix.
    /// </summary>
    public partial class BackfillClienteRoleForOrphanUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                INSERT INTO public.roles (id, dealer_id, name, description)
                SELECT gen_random_uuid(), d.dealer_id, 'Cliente', 'Cliente registrado'
                FROM (SELECT DISTINCT dealer_id FROM public.users
                      WHERE role_id = '00000000-0000-0000-0000-000000000000'
                        AND dealer_id <> '00000000-0000-0000-0000-000000000000') d
                WHERE NOT EXISTS (SELECT 1 FROM public.roles r WHERE r.dealer_id = d.dealer_id AND r.name = 'Cliente');

                UPDATE public.users u SET role_id = r.id FROM public.roles r
                WHERE u.role_id = '00000000-0000-0000-0000-000000000000'
                  AND u.dealer_id <> '00000000-0000-0000-0000-000000000000'
                  AND r.dealer_id = u.dealer_id AND r.name = 'Cliente';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Empty on purpose: reverting would recreate orphan tenant users with empty RoleId,
            // re-opening privilege escalation vulnerability (D1).
        }
    }
}

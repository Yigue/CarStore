using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <summary>
    /// spr PR 0.2 / REV-SDD-03 / BE-USERS-24: data-only migration. Before the fix in
    /// AssignRoleCommandHandler/CreateUserCommandHandler, nothing validated that a user's
    /// RoleId belonged to its own dealer (IDOR). This repairs any row that was already
    /// written with a cross-tenant RoleId by moving it to its own dealer's 'Cliente' role
    /// (creating that role first if the dealer does not have one yet), mirroring the
    /// pattern of 20260926140000_BackfillClienteRoleForOrphanUsers.
    /// </summary>
    public partial class FixCrossTenantRoleAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                INSERT INTO public.roles (id, dealer_id, name, description)
                SELECT gen_random_uuid(), d.dealer_id, 'Cliente', 'Cliente registrado'
                FROM (
                    SELECT DISTINCT u.dealer_id
                    FROM public.users u
                    JOIN public.roles r ON r.id = u.role_id
                    WHERE r.dealer_id <> u.dealer_id
                ) d
                WHERE NOT EXISTS (SELECT 1 FROM public.roles cr WHERE cr.dealer_id = d.dealer_id AND cr.name = 'Cliente');

                UPDATE public.users u
                SET role_id = cr.id
                FROM public.roles r, public.roles cr
                WHERE r.id = u.role_id
                  AND r.dealer_id <> u.dealer_id
                  AND cr.dealer_id = u.dealer_id
                  AND cr.name = 'Cliente';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Empty on purpose (qa A2): the original cross-tenant RoleId is gone once reassigned —
            // reverting would have to guess which foreign role each user used to point to, and
            // re-opening that IDOR is strictly worse than staying on 'Cliente'. Irreversible data fix.
        }
    }
}

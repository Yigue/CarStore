using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <summary>
    /// spr PR 4.2 / H9 (REV-SDD-01): data-only migration. GrantPermissionsCommandHandler wrote
    /// arbitrary permission strings straight into "UserPermissions" with zero validation against
    /// any catalog — a privilege-escalation path, since real enforcement goes through
    /// Role/RolePermission (spr PR 4.1's DefaultRoleCatalog). This removes the rows that belonged
    /// to TENANT users only (platform users have no Role at all and are identified exclusively by
    /// <c>users.role_id = '00000000-0000-0000-0000-000000000000'</c>; their legacy grants, if any,
    /// are left untouched).
    ///
    /// <para>
    /// REV-SDD-01: the criterion is <c>users.role_id &lt;&gt; Guid.Empty</c>, NOT
    /// <c>"UserPermissions".dealer_id</c> — that column is never set by
    /// <see cref="Domain.Users.UserPermission"/>'s constructor (it never calls
    /// <c>Entity.SetDealer</c>), so it is <c>Guid.Empty</c> on every single row regardless of
    /// which dealer the grant actually belongs to. Filtering by it would delete nothing (or
    /// everything the wrong way), which is exactly the review finding this PR's own task note
    /// corrects.
    /// </para>
    ///
    /// <para>
    /// Rows are copied into "user_permissions_legacy_backup" before deletion so <see cref="Down"/>
    /// can restore them exactly.
    /// </para>
    /// </summary>
    public partial class RemoveTenantUserPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "user_permissions_legacy_backup",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    dealer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    permission = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    granted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    granted_by = table.Column<Guid>(type: "uuid", nullable: true),
                    backed_up_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_permissions_legacy_backup", x => x.id);
                });

            migrationBuilder.Sql(@"
                INSERT INTO public.user_permissions_legacy_backup (id, user_id, dealer_id, permission, granted_at, granted_by)
                SELECT up.id, up.user_id, up.dealer_id, up.permission, up.granted_at, up.granted_by
                FROM public.""UserPermissions"" up
                JOIN public.users u ON u.id = up.user_id
                WHERE u.role_id <> '00000000-0000-0000-0000-000000000000';

                DELETE FROM public.""UserPermissions"" up
                USING public.users u
                WHERE u.id = up.user_id
                  AND u.role_id <> '00000000-0000-0000-0000-000000000000';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                INSERT INTO public.""UserPermissions"" (id, user_id, dealer_id, permission, granted_at, granted_by)
                SELECT id, user_id, dealer_id, permission, granted_at, granted_by
                FROM public.user_permissions_legacy_backup
                ON CONFLICT DO NOTHING;
            ");

            migrationBuilder.DropTable(
                name: "user_permissions_legacy_backup",
                schema: "public");
        }
    }
}

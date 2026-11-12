using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOwnerUserId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "owner_user_id",
                schema: "public",
                table: "dealer_settings",
                type: "uuid",
                nullable: true);

            // sfc A4.3.3 / C17 backfill: the owner of an existing dealer is its oldest ACTIVE Admin (ties by id).
            // A dealer without an active Admin keeps NULL and fails closed on destructive actions.
            migrationBuilder.Sql(@"
UPDATE public.dealer_settings AS ds
SET owner_user_id = (
    SELECT u.id
    FROM public.users AS u
    JOIN public.roles AS r ON r.id = u.role_id AND r.dealer_id = u.dealer_id
    WHERE u.dealer_id = ds.dealer_id
      AND u.is_active
      AND r.name = 'Admin'
    ORDER BY u.created_at, u.id
    LIMIT 1)
WHERE ds.owner_user_id IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "owner_user_id",
                schema: "public",
                table: "dealer_settings");
        }
    }
}

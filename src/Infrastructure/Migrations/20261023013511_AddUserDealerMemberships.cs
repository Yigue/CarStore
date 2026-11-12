using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserDealerMemberships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "user_dealer_memberships",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    granted_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    dealer_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_dealer_memberships", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_user_dealer_memberships_user_id",
                schema: "public",
                table: "user_dealer_memberships",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ux_user_dealer_memberships_user_id_dealer_id",
                schema: "public",
                table: "user_dealer_memberships",
                columns: new[] { "user_id", "dealer_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_dealer_memberships",
                schema: "public");
        }
    }
}

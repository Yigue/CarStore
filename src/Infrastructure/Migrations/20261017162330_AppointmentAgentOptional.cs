using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AppointmentAgentOptional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "agent_id",
                schema: "public",
                table: "appointments",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // sfc B5.2.3 / H15: Requested appointments have no agent and cannot exist in the old
            // model (agent_id NOT NULL), so they are deleted before the column is tightened.
            migrationBuilder.Sql("DELETE FROM public.appointments WHERE agent_id IS NULL;");

            migrationBuilder.AlterColumn<Guid>(
                name: "agent_id",
                schema: "public",
                table: "appointments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}

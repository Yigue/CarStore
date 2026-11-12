using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptionPlanCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "plan_code",
                schema: "public",
                table: "dealer_subscriptions",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            // spr PR 1.1 (SPR-02/B1, D1): "plan único" — every row that predates this column is backfilled
            // to the only plan that has ever existed. Rows already carrying a value (none today) are left alone.
            migrationBuilder.Sql(
                "UPDATE public.dealer_subscriptions SET plan_code = 'standard' WHERE plan_code IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "plan_code",
                schema: "public",
                table: "dealer_subscriptions");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <summary>
    /// REQ-FIN-PRECISION-001: widen <c>transactions.amount</c> to
    /// <c>numeric(18, 2)</c> so every read returns at most 2 decimal places.
    ///
    /// PRE-DEPLOY STEP (run BEFORE applying this migration):
    ///   psql -f scripts/round-transaction-amounts.sql
    /// Idempotent — only rounds rows whose amount has more than 2 decimals.
    ///
    /// Reversible: Down() reverts to <c>numeric</c> (unbounded precision).
    /// Any value already at 2dp round-trips losslessly. Values with more than
    /// 2dp were already rounded by the pre-deploy script.
    /// </summary>
    public partial class AddMoneyPrecision : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "amount",
                schema: "public",
                table: "transactions",
                type: "numeric(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "amount",
                schema: "public",
                table: "transactions",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)");
        }
    }
}
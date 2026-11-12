using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBillingReconciliationFlagsAndBackfillSubscriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "billing_reconciliation_flags",
                schema: "public",
                columns: table => new
                {
                    dealer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    flagged_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    cleared_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_billing_reconciliation_flags", x => x.dealer_id);
                });

            // spr ADR B8 / task 3.1.12 — BACKFILL, in the same migration as the fail-closed switch.
            //
            // SubscriptionGuardMiddleware now answers 402 to a dealer with no dealer_subscriptions
            // row (it used to let it through). Every existing dealer that has no row gets one here,
            // as 'Incomplete' (the same state provisioning now creates), BEFORE the new API version
            // serves traffic (the deploy runs migrations first). Consequences, by design:
            //   * dealers that already have a row are untouched (NOT EXISTS) and keep their status;
            //   * a dealer with no row IS blocked until it pays — "no row" is no longer a free pass;
            //   * current_period_start = the dealer's created_at, so the reconciliation job
            //     (ReconcileSubscriptionsJob) measures the grace from when the dealer really appeared
            //     and can flag it, instead of treating all of them as brand new;
            //   * stripe_customer_id is carried over from dealer_settings so the webhook can find the row.
            // Idempotent: re-running inserts nothing. Operators: review
            //   SELECT dealer_id FROM dealer_settings ds WHERE NOT EXISTS (...)
            // BEFORE deploying, and mark any dealer that must keep working (e.g. billed outside Stripe).
            migrationBuilder.Sql(@"
INSERT INTO public.dealer_subscriptions
    (id, dealer_id, stripe_customer_id, stripe_subscription_id, status, trial_ends_at,
     current_period_start, current_period_end, cancelled_at, plan_id)
SELECT gen_random_uuid(), ds.dealer_id, LEFT(ds.stripe_customer_id, 64), NULL, 'Incomplete', NULL,
       ds.created_at, ds.created_at, NULL, ''
FROM public.dealer_settings ds
WHERE NOT EXISTS (
    SELECT 1 FROM public.dealer_subscriptions s WHERE s.dealer_id = ds.dealer_id
);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The backfilled 'Incomplete' rows are intentionally NOT deleted on rollback: they are
            // indistinguishable from rows provisioning creates, and removing a subscription row is
            // exactly the destructive action billing reconciliation (B9) must never take.
            migrationBuilder.DropTable(
                name: "billing_reconciliation_flags",
                schema: "public");
        }
    }
}

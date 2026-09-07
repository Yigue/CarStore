using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <summary>
    /// One accepted quote per car.
    ///
    /// <para>
    /// A car can carry any number of competing offers — that is the normal shape of a
    /// negotiation — but accepting one is the dealership committing the unit to a buyer, and a
    /// unit can only be committed once. AcceptQuoteCommandHandler checks this, but a check that
    /// reads and then writes has no protection against two requests landing in between; the
    /// index is what makes the rule hold.
    /// </para>
    /// </summary>
    public partial class AddOneAcceptedQuotePerCarIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Existing data predates the rule. Until now the car was reserved when a quote was
            // RAISED, so a second acceptance was only reachable by releasing the car first
            // (reject/delete/expire) and quoting it again — which is exactly how rows with
            // several accepted quotes on one car came to exist.
            //
            // The unique index cannot be created over them, and a migration that fails on real
            // data is a landmine for every environment it has not run in yet. So resolve them
            // here, deterministically: for each car keep the most recently accepted quote and
            // mark the earlier ones Rejected, which is what they factually became the moment a
            // later one was accepted.
            //
            // Rejected — not deleted: the offer happened and the history is worth keeping. The
            // reason goes into `comments` because the schema has no column for it (Quote.Reject
            // only carries the reason in its domain event), and comments is what the operator
            // actually reads on the quote.
            migrationBuilder.Sql("""
                UPDATE public.quotes AS q
                SET status = 'Rejected',
                    comments = left(
                        'Rechazada automáticamente: otra cotización fue aceptada para este vehículo. '
                        || coalesce(q.comments, ''), 500)
                WHERE q.status = 'Accepted'
                  AND q.is_deleted = false
                  AND EXISTS (
                      SELECT 1
                      FROM public.quotes AS newer
                      WHERE newer.car_id = q.car_id
                        AND newer.status = 'Accepted'
                        AND newer.is_deleted = false
                        AND (newer.updated_at, newer.id) > (q.updated_at, q.id)
                  );
                """);

            migrationBuilder.CreateIndex(
                name: "ux_quotes_one_accepted_per_car",
                schema: "public",
                table: "quotes",
                column: "car_id",
                unique: true,
                filter: "status = 'Accepted' AND is_deleted = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Only the index comes back off. The quotes this migration rejected are not restored:
            // which ones they were is not recorded anywhere, and re-accepting them would put the
            // same car under two commitments again.
            migrationBuilder.DropIndex(
                name: "ux_quotes_one_accepted_per_car",
                schema: "public",
                table: "quotes");
        }
    }
}

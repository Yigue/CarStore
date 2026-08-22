using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// Reconciles two mappings that reached the model without their own migration.
    ///
    /// <para>
    /// <c>appointments.status</c> — <c>AppointmentConfiguration</c> maps
    /// <c>Appointment.Status</c> and <c>GetAppointmentsQueryHandler</c> projects it, but the
    /// column was back-patched into the already-applied <c>20260527221933_AddAppointments</c>
    /// migration. Databases that had recorded that migration never received the column and
    /// answered every <c>GET /api/v1/appointments</c> with Postgres 42703. That back-patch has
    /// been reverted, so the column now ships here.
    /// </para>
    ///
    /// <para>
    /// <c>sales</c> indexes — <c>SaleConfiguration</c> replaced the plain <c>ix_sales_car_id</c>
    /// with the partial unique <c>ux_sales_one_completed_per_car</c>, but
    /// <c>20260802020000_AddOneCompletedSalePerCarIndex</c> was hand-written and left the model
    /// snapshot stale, so the drop never shipped.
    /// </para>
    ///
    /// <para>
    /// Every statement is idempotent because the target databases are in three different states:
    /// created before the back-patch (no column), created after it (column already present via
    /// the then-edited migration), or created after this migration (clean).
    /// </para>
    /// </summary>
    public partial class AddAppointmentStatusColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE public.appointments
                ADD COLUMN IF NOT EXISTS status character varying(20) NOT NULL DEFAULT 'Scheduled';
                """);

            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS public.ix_sales_car_id;
                """);

            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX IF NOT EXISTS ux_sales_one_completed_per_car
                ON public.sales (car_id)
                WHERE status = 'Completed';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS public.ux_sales_one_completed_per_car;
                """);

            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS ix_sales_car_id
                ON public.sales (car_id);
                """);

            migrationBuilder.Sql("""
                ALTER TABLE public.appointments DROP COLUMN IF EXISTS status;
                """);
        }
    }
}

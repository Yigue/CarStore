using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Tenancy;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Appointments.Queries.GetAppointments;

/// <summary>
/// Range query for the calendar view. Resolves vehicle / client / agent names via
/// LEFT JOINs so the API stays calendar-friendly (FullCalendar expects pre-joined strings).
/// Tenant scoping is enforced by the global query filter on <c>Appointment</c>;
/// the explicit <c>DealerId</c> predicate is belt-and-suspenders.
/// </summary>
internal sealed class GetAppointmentsQueryHandler(
    IApplicationDbContext context,
    ICurrentTenantService tenantService)
    : IQueryHandler<GetAppointmentsQuery, IReadOnlyList<AppointmentDto>>
{
    public async Task<Result<IReadOnlyList<AppointmentDto>>> Handle(
        GetAppointmentsQuery query,
        CancellationToken cancellationToken)
    {
        Guid dealerId = tenantService.DealerId;

        // Car display name comes from the Marca + Modelo + Anio (year) triple — the
        // domain model stores brand/model as related catalog entities, not strings.
        var rows = await (
            from a in context.Appointments.AsNoTracking()
            where a.DealerId == dealerId
                  && a.StartDateTime < query.To
                  && a.EndDateTime > query.From
            join car in context.Cars.IgnoreQueryFilters().AsNoTracking()
                on a.VehicleId equals car.Id into carJoin
            from car in carJoin.DefaultIfEmpty()
            join marca in context.Marca.AsNoTracking()
                on (car != null ? car.MarcaId : Guid.Empty) equals marca.Id into marcaJoin
            from marca in marcaJoin.DefaultIfEmpty()
            join modelo in context.Modelo.AsNoTracking()
                on (car != null ? car.ModeloId : Guid.Empty) equals modelo.Id into modeloJoin
            from modelo in modeloJoin.DefaultIfEmpty()
            join client in context.Clients.IgnoreQueryFilters().AsNoTracking()
                on a.ClientId equals (Guid?)client.Id into clientJoin
            from client in clientJoin.DefaultIfEmpty()
            join lead in context.Leads.IgnoreQueryFilters().AsNoTracking()
                on a.LeadId equals (Guid?)lead.Id into leadJoin
            from lead in leadJoin.DefaultIfEmpty()
            join agent in context.Users.IgnoreQueryFilters().AsNoTracking()
                on a.AgentId equals agent.Id into agentJoin
            from agent in agentJoin.DefaultIfEmpty()
            orderby a.StartDateTime
            select new AppointmentDto(
                a.Id,
                a.VehicleId,
                car != null
                    ? ((marca != null ? marca.Nombre : "")
                        + " "
                        + (modelo != null ? modelo.Nombre : "")
                        + " "
                        + car.Anio).Trim()
                    : null,
                a.ClientId,
                client != null ? (client.FirstName + " " + client.LastName) : (lead != null ? lead.ClientName : null),
                a.AgentId,
                agent != null ? (agent.FirstName + " " + agent.LastName) : null,
                a.StartDateTime,
                a.EndDateTime,
                a.Type,
                a.Type.ToString(),
                a.Notes,
                a.CreatedAt))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<AppointmentDto>>(rows);
    }
}

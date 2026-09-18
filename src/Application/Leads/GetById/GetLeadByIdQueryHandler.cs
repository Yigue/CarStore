using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Leads;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Leads.GetById;

internal sealed class GetLeadByIdQueryHandler(IApplicationDbContext context)
    : IQueryHandler<GetLeadByIdQuery, LeadDetailResponse>
{
    public async Task<Result<LeadDetailResponse>> Handle(GetLeadByIdQuery query, CancellationToken cancellationToken)
    {
        var result = await (
            from l in context.Leads.IgnoreQueryFilters()
            join u in context.Users.IgnoreQueryFilters() on l.AssignedAgentId equals (Guid?)u.Id into agentJoin
            from agent in agentJoin.DefaultIfEmpty()
            join c in context.Cars on l.InterestedVehicleId equals (Guid?)c.Id into carJoin
            from car in carJoin.DefaultIfEmpty()
            join marca in context.Marca on car.MarcaId equals marca.Id into marcaJoin
            from m in marcaJoin.DefaultIfEmpty()
            join modelo in context.Modelo on car.ModeloId equals modelo.Id into modeloJoin
            from md in modeloJoin.DefaultIfEmpty()
            where l.Id == query.LeadId
            select new LeadDetailResponse(
                l.Id,
                l.ClientName,
                EF.Property<string>(l, "Email"),
                l.Phone,
                l.Status,
                l.Status.ToString(),
                l.AssignedAgentId,
                agent != null ? agent.FirstName + " " + agent.LastName : null,
                l.InterestedVehicleId,
                car != null ? m.Nombre + " " + md.Nombre + " " + car.Anio : null,
                l.ConvertedClientId,
                l.LossReason,
                l.Notes,
                l.Source.ToString(),
                l.CreatedAt
            )
        ).FirstOrDefaultAsync(cancellationToken);

        if (result is null)
        {
            return Result.Failure<LeadDetailResponse>(LeadErrors.NotFound(query.LeadId));
        }

        // LEAD-05: the related records, so the detail view can link to each of them.
        //
        // Loaded in separate round-trips on purpose. Folding four one-to-many collections into
        // the projection above would multiply the rows by each other (three quotes and two
        // appointments give six rows of the same lead) and then need de-duplicating in memory,
        // which is slower AND harder to read than four small, indexed queries.
        return Result.Success(result with
        {
            Client = await LoadClientAsync(result.ConvertedClientId, cancellationToken),
            Quotes = await LoadQuotesAsync(result.Id, result.ConvertedClientId, cancellationToken),
            Appointments = await LoadAppointmentsAsync(result.Id, result.ConvertedClientId, cancellationToken),
            Sales = await LoadSalesAsync(result.Id, result.ConvertedClientId, cancellationToken),
        });
    }

    private async Task<LeadClientDto?> LoadClientAsync(Guid? clientId, CancellationToken cancellationToken)
    {
        if (clientId is not { } id)
        {
            return null;
        }

        return await context.Clients
            .Where(c => c.Id == id)
            .Select(c => new LeadClientDto(
                c.Id,
                c.FirstName + " " + c.LastName,
                EF.Property<string>(c, "Email"),
                c.Phone,
                c.Status.ToString()))
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <remarks>
    /// Matched on the lead OR on the client it became: paperwork raised before the conversion
    /// carries the lead, paperwork raised after carries the client, and both belong to the same
    /// person. Matching on only one half is how a converted lead ends up looking like it never
    /// had a quote.
    /// </remarks>
    private async Task<IReadOnlyList<LeadQuoteDto>> LoadQuotesAsync(
        Guid leadId,
        Guid? clientId,
        CancellationToken cancellationToken) =>
        await (
            from q in context.Quotes
            join car in context.Cars on q.CarId equals car.Id
            join marca in context.Marca on car.MarcaId equals marca.Id
            join modelo in context.Modelo on car.ModeloId equals modelo.Id
            where q.LeadId == leadId || (clientId != null && q.ClientId == clientId)
            orderby q.CreatedAt descending
            select new LeadQuoteDto(
                q.Id,
                q.CarId,
                marca.Nombre + " " + modelo.Nombre + " " + car.Anio,
                q.ProposedPrice.Amount,
                q.Status.ToString(),
                q.CreatedAt)
        ).ToListAsync(cancellationToken);

    private async Task<IReadOnlyList<LeadAppointmentDto>> LoadAppointmentsAsync(
        Guid leadId,
        Guid? clientId,
        CancellationToken cancellationToken) =>
        await context.Appointments
            .Where(a => a.LeadId == leadId || (clientId != null && a.ClientId == clientId))
            .OrderByDescending(a => a.StartDateTime)
            .Select(a => new LeadAppointmentDto(
                a.Id,
                a.StartDateTime,
                a.Type.ToString(),
                a.Status.ToString(),
                a.Notes ?? string.Empty))
            .ToListAsync(cancellationToken);

    private async Task<IReadOnlyList<LeadSaleDto>> LoadSalesAsync(
        Guid leadId,
        Guid? clientId,
        CancellationToken cancellationToken) =>
        await (
            from s in context.Sales
            join car in context.Cars on s.CarId equals car.Id
            join marca in context.Marca on car.MarcaId equals marca.Id
            join modelo in context.Modelo on car.ModeloId equals modelo.Id
            where s.LeadId == leadId || (clientId != null && s.ClientId == clientId)
            orderby s.SaleDate descending
            select new LeadSaleDto(
                s.Id,
                s.CarId,
                marca.Nombre + " " + modelo.Nombre + " " + car.Anio,
                s.FinalPrice.Amount,
                s.Status.ToString(),
                s.SaleDate)
        ).ToListAsync(cancellationToken);
}

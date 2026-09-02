using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Quotes.Get;

internal sealed class GetQuotesQueryHandler(IApplicationDbContext context)
    : IQueryHandler<GetQuotesQuery, List<QuoteResponse>>
{
    public async Task<Result<List<QuoteResponse>>> Handle(GetQuotesQuery query, CancellationToken cancellationToken)
    {
        IQueryable<Domain.Quotes.Quote> source = context.Quotes;

        if (query.ClientId is { } clientId)
        {
            // A quote raised before enquiries created leads hangs off the client directly; one
            // raised after hangs off the lead the client was converted from. Both are this
            // client's history, so match either.
            source = source.Where(q =>
                q.ClientId == clientId
                || (q.Lead != null && q.Lead.ConvertedClientId == clientId));
        }

        if (query.LeadId is { } leadId)
        {
            source = source.Where(q => q.LeadId == leadId);
        }

        if (query.CarId is { } carId)
        {
            source = source.Where(q => q.CarId == carId);
        }

        List<QuoteResponse> quotes = await source
            .Include(q => q.Car)
                .ThenInclude(c => c.Marca)
            .Include(q => q.Car)
                .ThenInclude(c => c.Modelo)
            .Include(q => q.Client)
            .Include(q => q.Lead)
            .Select(quote => new QuoteResponse
            {
                Id = quote.Id,
                CarId = quote.CarId,
                ClientId = quote.ClientId,
                LeadId = quote.LeadId,
                ProposedPrice = quote.ProposedPrice.Amount,
                PaymentMethod = quote.PaymentMethod.ToString(),
                Status = quote.Status.ToString(),
                ValidUntil = quote.ValidUntil,
                Comments = quote.Comments,
                CreatedAt = quote.CreatedAt,
                UpdatedAt = quote.UpdatedAt,
                CarBrand = quote.Car.Marca.Nombre,
                CarModel = quote.Car.Modelo.Nombre,
                ClientName = quote.Client != null ? $"{quote.Client.FirstName} {quote.Client.LastName}" : (quote.Lead != null ? quote.Lead.ClientName : "Desconocido"),
                OriginLeadId = quote.Client != null ? quote.Client.OriginLeadId : null,
                ConvertedClientId = quote.Lead != null ? quote.Lead.ConvertedClientId : null
            })
            .ToListAsync(cancellationToken);

        return quotes;
    }
}

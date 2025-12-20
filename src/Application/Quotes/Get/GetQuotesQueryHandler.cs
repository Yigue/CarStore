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
        List<QuoteResponse> quotes = await context.Quotes
            .Include(q => q.Car)
                .ThenInclude(c => c.Marca)
            .Include(q => q.Car)
                .ThenInclude(c => c.Modelo)
            .Include(q => q.Client)
            .Select(quote => new QuoteResponse
            {
                Id = quote.Id,
                CarId = quote.CarId,
                ClientId = quote.ClientId,
                ProposedPrice = quote.ProposedPrice.Amount,
                Status = quote.Status.ToString(),
                ValidUntil = quote.ValidUntil,
                Comments = quote.Comments,
                CreatedAt = quote.CreatedAt,
                UpdatedAt = quote.UpdatedAt,
                CarBrand = quote.Car.Marca.Nombre,
                CarModel = quote.Car.Modelo.Nombre,
                ClientName = $"{quote.Client.FirstName} {quote.Client.LastName}"
            })
            .ToListAsync(cancellationToken);

        return quotes;
    }
}

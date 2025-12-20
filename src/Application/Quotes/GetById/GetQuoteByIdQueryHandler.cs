using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Quotes.Get;
using Domain.Quotes;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Quotes.GetById;

internal sealed class GetQuoteByIdQueryHandler(IApplicationDbContext context)
    : IQueryHandler<GetQuoteByIdQuery, QuoteResponse>
{
    public async Task<Result<QuoteResponse>> Handle(GetQuoteByIdQuery query, CancellationToken cancellationToken)
    {
        Quote? quote = await context.Quotes
            .Include(q => q.Car)
                .ThenInclude(c => c.Marca)
            .Include(q => q.Car)
                .ThenInclude(c => c.Modelo)
            .Include(q => q.Client)
            .FirstOrDefaultAsync(q => q.Id == query.QuoteId, cancellationToken);

        if (quote is null)
        {
            return Result.Failure<QuoteResponse>(QuoteErrors.NotFound(query.QuoteId));
        }

        var response = new QuoteResponse
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
        };

        return response;
    }
}

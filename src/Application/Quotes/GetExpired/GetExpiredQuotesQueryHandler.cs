using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Quotes.Get;
using Domain.Quotes.Attributes;
using Domain.Shared.Specifications;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Quotes.GetExpired;

internal sealed class GetExpiredQuotesQueryHandler(
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider)
    : IQueryHandler<GetExpiredQuotesQuery, List<QuoteResponse>>
{
    public async Task<Result<List<QuoteResponse>>> Handle(GetExpiredQuotesQuery query, CancellationToken cancellationToken)
    {
        var specification = new ExpiredQuotesSpecification(dateTimeProvider);
        
        var quotes = await context.Quotes
            .Where(specification.ToExpression())
            .Include(q => q.Car)
                .ThenInclude(c => c.Marca)
            .Include(q => q.Car)
                .ThenInclude(c => c.Modelo)
            .Include(q => q.Client)
            .ToListAsync(cancellationToken);

        List<QuoteResponse> response = quotes.Select(quote => new QuoteResponse
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
            CarBrand = quote.Car?.Marca?.Nombre ?? string.Empty,
            CarModel = quote.Car?.Modelo?.Nombre ?? string.Empty,
            ClientName = quote.Client != null ? $"{quote.Client.FirstName} {quote.Client.LastName}" : string.Empty
        }).ToList();

        return response;
    }
}


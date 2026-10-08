using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Tenancy;
using Application.Common;
using Application.Quotes.Get;
using Domain.Quotes;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Quotes.GetById;

internal sealed class GetQuoteByIdQueryHandler(
    IApplicationDbContext context,
    ICurrentTenantService? tenantService = null)
    : IQueryHandler<GetQuoteByIdQuery, QuoteResponse>
{
    public async Task<Result<QuoteResponse>> Handle(GetQuoteByIdQuery query, CancellationToken cancellationToken)
    {
        if (tenantService is not null)
        {
            Result guard = TenantGuard.EnsureHasTenant(tenantService);
            if (guard.IsFailure)
            {
                return Result.Failure<QuoteResponse>(guard.Error);
            }
        }

        Quote? quote = await context.Quotes
            .Include(q => q.Car)
                .ThenInclude(c => c.Marca)
            .Include(q => q.Car)
                .ThenInclude(c => c.Modelo)
            .Include(q => q.Client)
            .Include(q => q.Lead)
            .FirstOrDefaultAsync(q => q.Id == query.QuoteId, cancellationToken);

        if (quote is null)
        {
            return Result.Failure<QuoteResponse>(QuoteErrors.NotFound(query.QuoteId));
        }

        if (tenantService is not null)
        {
            Result dealerCheck = TenantGuard.EnsureSameDealerOrNotFound(tenantService, quote.DealerId, QuoteErrors.NotFound(query.QuoteId));
            if (dealerCheck.IsFailure)
            {
                return Result.Failure<QuoteResponse>(dealerCheck.Error);
            }
        }

        var response = new QuoteResponse
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
        };

        return response;
    }
}

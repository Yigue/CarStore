using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Quotes.Get;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Quotes.GetMy;

internal sealed class GetMyQuotesQueryHandler(
    IApplicationDbContext context,
    IUserContext userContext)
    : IQueryHandler<GetMyQuotesQuery, List<QuoteResponse>>
{
    public async Task<Result<List<QuoteResponse>>> Handle(GetMyQuotesQuery query, CancellationToken cancellationToken)
    {
        // 1. Obtener el usuario actual
        var user = await context.Users
            .SingleOrDefaultAsync(u => u.Id == userContext.UserId, cancellationToken);

        if (user is null)
        {
            return Result.Failure<List<QuoteResponse>>(Error.NotFound("User.NotFound", "Usuario no encontrado."));
        }

        // 2. Obtener cotizaciones filtradas por el email del usuario (que actúa como Cliente)
        // NOTA: En un sistema más maduro, habría una FK directa User -> Client.
        var quotes = await context.Quotes
            .Include(q => q.Car)
                .ThenInclude(c => c.Marca)
            .Include(q => q.Car)
                .ThenInclude(c => c.Modelo)
            .Include(q => q.Client)
            .Include(q => q.Lead)
            .Where(q => q.Client != null && q.Client.Email.Value == user.Email.Value)
            .Select(quote => new QuoteResponse
            {
                Id = quote.Id,
                CarId = quote.CarId,
                ClientId = quote.ClientId ?? Guid.Empty,
                ProposedPrice = quote.ProposedPrice.Amount,
                Status = quote.Status.ToString(),
                ValidUntil = quote.ValidUntil,
                Comments = quote.Comments,
                CreatedAt = quote.CreatedAt,
                UpdatedAt = quote.UpdatedAt,
                CarBrand = quote.Car.Marca.Nombre,
                CarModel = quote.Car.Modelo.Nombre,
                ClientName = quote.Client != null ? $"{quote.Client.FirstName} {quote.Client.LastName}" : (quote.Lead != null ? quote.Lead.ClientName : "Desconocido")
            })
            .ToListAsync(cancellationToken);

        return quotes;
    }
}

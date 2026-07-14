using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Sales.Get;

internal sealed class GetSalesQueryHandler(IApplicationDbContext context)
    : IQueryHandler<GetSalesQuery, List<SaleResponse>>
{
    public async Task<Result<List<SaleResponse>>> Handle(GetSalesQuery query, CancellationToken cancellationToken)
    {
        List<SaleResponse> sales = await context.Sales
            .Include(s => s.Car)
            .Include(s => s.Client)
            .Select(sale => new SaleResponse
            {
                Id = sale.Id,
                CarId = sale.CarId,
                ClientId = sale.ClientId,
                QuoteId = sale.QuoteId,
                LeadId = sale.LeadId,
                SalespersonId = sale.SalespersonId,
                FinalPrice = sale.FinalPrice.Amount,
                PaymentMethod = sale.PaymentMethod.ToString(),
                Status = sale.Status.ToString(),
                ContractNumber = sale.ContractNumber,
                SaleDate = sale.SaleDate,
                Comments = sale.Comments,
                CarBrand = sale.Car.Marca.Nombre,
                CarModel = sale.Car.Modelo.Nombre,
                ClientName = $"{sale.Client.FirstName} {sale.Client.LastName}",
                // No navigation property to User (mirrors the no-hard-FK convention on
                // SalespersonId) — resolved as a correlated subquery instead.
                SalespersonName = context.Users
                    .Where(u => u.Id == sale.SalespersonId)
                    .Select(u => u.FirstName + " " + u.LastName)
                    .FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        return sales;
    }
}

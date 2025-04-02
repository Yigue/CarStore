using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Sales.Get;
using Domain.Sales;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Sales.GetById;

internal sealed class GetSaleByIdQueryHandler(IApplicationDbContext context)
    : IQueryHandler<GetSaleByIdQuery, SaleResponse>
{
    public async Task<Result<SaleResponse>> Handle(GetSaleByIdQuery query, CancellationToken cancellationToken)
    {
        Sale? sale = await context.Sales
            .Include(s => s.Car)
            .Include(s => s.Client)
            .FirstOrDefaultAsync(s => s.Id == query.Id, cancellationToken);

        if (sale is null)
        {
            return Result.Failure<SaleResponse>(SalesErrors.NotFound(query.Id));
        }

        var response = new SaleResponse
        {
            Id = sale.Id,
            CarId = sale.CarId,
            ClientId = sale.ClientId,
            FinalPrice = sale.FinalPrice,
            PaymentMethod = sale.PaymentMethod.ToString(),
            Status = sale.Status.ToString(),
            ContractNumber = sale.ContractNumber,
            SaleDate = sale.SaleDate,
            Comments = sale.Comments,
            CarBrand = sale.Car.Marca.Nombre,
            CarModel = sale.Car.Modelo.Nombre,
            ClientName = $"{sale.Client.FirstName} {sale.Client.LastName}",
        };

        return response;
    }
}

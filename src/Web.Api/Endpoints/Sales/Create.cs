using Application.Sales.Create;
using Domain.Financial.Attributes;
using Domain.Sales;
using Domain.Sales.Attributes;
using MediatR;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Sales;

internal sealed class Create : IEndpoint
{
    public sealed record Request(
        Guid CarId,
        Guid ClientId,
        // VEN-04: omit both when converting an accepted quote and the sale inherits the price and
        // the payment arrangement that were actually agreed. Required otherwise.
        decimal? FinalPrice,
        PaymentMethod? PaymentMethod,
        string ContractNumber,
        string Comments,
        // Optional initial status. Null/omitted leaves the sale Pending — it is
        // only force-completed when the caller explicitly requests Completed.
        SaleStatus? Status = null,
        Guid? LeadId = null,
        Guid? QuoteId = null,
        Guid? SalespersonId = null,

        // VEN-02: the identity a prospect created by the CRM is still missing. Required only when
        // the client cannot be invoiced yet.
        string? ClientDni = null,
        string? ClientAddress = null,

        // VEN-03: how the operation is paid.
        decimal? DownPayment = null,
        Guid? TradeInCarId = null,
        decimal? TradeInValue = null,
        decimal? FinancedAmount = null,
        int? InstallmentCount = null,
        decimal? InstallmentAmount = null,
        string? FinancingEntity = null,

        // VEN-03: the paperwork numbers.
        string? InvoiceNumber = null,
        string? TransferFormNumber = null,
        string? RegistrationNumber = null,
        DateTime? DeliveryDate = null);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("sales", async (Request request, ISender sender, CancellationToken cancellationToken) =>
        {
            // Named arguments, enforced by ArchitectureTests.CommandConstructionTests: this call
            // now passes eight strings and nine nullables in a row, and positionally a swapped
            // pair of them compiles, ships, and writes the transfer-form number into the invoice
            // field. The names are what makes the mistake impossible.
            var command = new CreateSaleCommand(
                CarId: request.CarId,
                ClientId: request.ClientId,
                FinalPrice: request.FinalPrice,
                PaymentMethod: request.PaymentMethod,
                ContractNumber: request.ContractNumber,
                Comments: request.Comments,
                LeadId: request.LeadId,
                QuoteId: request.QuoteId,
                Status: request.Status,
                SalespersonId: request.SalespersonId,
                ClientDni: request.ClientDni,
                ClientAddress: request.ClientAddress,
                DownPayment: request.DownPayment,
                TradeInCarId: request.TradeInCarId,
                TradeInValue: request.TradeInValue,
                FinancedAmount: request.FinancedAmount,
                InstallmentCount: request.InstallmentCount,
                InstallmentAmount: request.InstallmentAmount,
                FinancingEntity: request.FinancingEntity,
                InvoiceNumber: request.InvoiceNumber,
                TransferFormNumber: request.TransferFormNumber,
                RegistrationNumber: request.RegistrationNumber,
                DeliveryDate: request.DeliveryDate);

            Result<Guid> result = await sender.Send(command, cancellationToken);

            return result.Match(
                id => Results.Created($"/sales/{id}", new { id }),
                CustomResults.Problem);
        })
        .HasPermission(Permissions.SalesCreate)
        .WithTags(Tags.Sales)
        .WithName("CreateSale")
        .Produces<Guid>(StatusCodes.Status201Created)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}


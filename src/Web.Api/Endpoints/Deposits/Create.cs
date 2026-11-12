using Application.Deposits.Create;
using Domain.Financial.Attributes;
using MediatR;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Deposits;

/// <summary>sfc B1.3.5 / RN-001..RN-004: records a seña over a car and reserves it.</summary>
internal sealed class Create : IEndpoint
{
    public sealed record Request(
        Guid CarId,
        Guid? ClientId,
        Guid? LeadId,
        Guid? QuoteId,
        decimal Amount,
        string Currency,
        PaymentMethod PaymentMethod = PaymentMethod.Cash);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("deposits", async (Request request, ISender sender, CancellationToken cancellationToken) =>
        {
            var command = new CreateDepositCommand(
                CarId: request.CarId,
                ClientId: request.ClientId,
                LeadId: request.LeadId,
                QuoteId: request.QuoteId,
                Amount: request.Amount,
                Currency: request.Currency,
                PaymentMethod: request.PaymentMethod);

            Result<Guid> result = await sender.Send(command, cancellationToken);

            return result.Match(
                id => Results.Created($"/deposits/{id}", new { id }),
                CustomResults.Problem);
        })
        .HasPermission("sales:create")
        .WithTags(Tags.Sales)
        .WithName("CreateDeposit")
        .Produces(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);
    }
}

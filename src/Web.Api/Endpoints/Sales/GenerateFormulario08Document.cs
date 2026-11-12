using Application.Sales.GenerateDocument;
using MediatR;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Sales;

/// <summary>
/// sfc B3.2 / B8 / RN-028: generates the Formulario 08 pre-filled data sheet for a sale — a
/// reference sheet with the transfer data, NOT the official DNRPA form (security elements
/// CarStore cannot reproduce).
/// </summary>
internal sealed class GenerateFormulario08Document : IEndpoint
{
    public sealed record Response(Guid DocumentId, string Sha256Hash, int Version);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("sales/{id:guid}/documents/formulario08", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
        {
            var command = new GenerateSaleDocumentCommand(id, SaleDocumentKind.Formulario08);

            Result<GenerateSaleDocumentResult> result = await sender.Send(command, cancellationToken);

            return result.Match(
                value => Results.Created(
                    $"/sales/{id}/documents/{value.DocumentId}",
                    new Response(value.DocumentId, value.Sha256Hash, value.Version)),
                CustomResults.Problem);
        })
        .HasPermission(Permissions.SalesUpdate)
        .WithTags(Tags.Sales)
        .WithName("GenerateSaleFormulario08Document")
        .Produces<Response>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);
    }
}

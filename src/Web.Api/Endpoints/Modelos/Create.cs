using Application.Modelos.Create;
using MediatR;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Modelos;

public sealed class Create : IEndpoint
{
    public sealed record Request(string Nombre, Guid MarcaId);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("modelos", Handler)
            .WithTags(Tags.Modelos)
            .RequireAuthorization();
    }

    private static async Task<IResult> Handler(Request request, ISender sender, CancellationToken cancellationToken)
    {
        Result<Guid> result = await sender.Send(new CreateModeloCommand(request.Nombre, request.MarcaId), cancellationToken);

        return result.Match(id => Results.Created($"/api/v1/modelos/{id}", new { id }), CustomResults.Problem);
    }
}

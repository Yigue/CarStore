using Application.Marcas.Create;
using MediatR;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Marcas;

public sealed class Create : IEndpoint
{
    public sealed record Request(string Nombre);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("marcas", Handler)
            .WithTags(Tags.Marcas)
            .RequireAuthorization();
    }

    private static async Task<IResult> Handler(Request request, ISender sender, CancellationToken cancellationToken)
    {
        Result<Guid> result = await sender.Send(new CreateMarcaCommand(request.Nombre), cancellationToken);

        return result.Match(id => Results.Created($"/api/v1/marcas/{id}", new { id }), CustomResults.Problem);
    }
}

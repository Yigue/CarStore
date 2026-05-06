using Application.Marcas.Get;
using MediatR;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Marcas;

public sealed class Get : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("marcas", Handler)
            .WithTags(Tags.Marcas)
            .RequireAuthorization();
    }

    private static async Task<IResult> Handler(ISender sender, CancellationToken cancellationToken)
    {
        Result<List<Domain.Cars.Attributes.Marca>> result = await sender.Send(new GetMarcasQuery(), cancellationToken);

        return result.Match(Results.Ok, CustomResults.Problem);
    }
}

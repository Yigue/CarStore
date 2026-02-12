using Application.Financial.Categories.Delete;
using MediatR;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Financial.Categories;

public sealed class Delete : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapDelete("financial/categories/{id}", Handler)
            .WithTags(Tags.Financial)
            .RequireAuthorization();
    }

    private static async Task<IResult> Handler(Guid id, ISender sender, CancellationToken cancellationToken)
    {
        var command = new DeleteCategoryCommand(id);

        Result result = await sender.Send(command, cancellationToken);

        return result.Match(Results.NoContent, CustomResults.Problem);
    }
}

using Application.Financial.Categories.Update;
using Domain.Financial.Attributes;
using MediatR;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Financial.Categories;

public sealed class Update : IEndpoint
{
    public sealed record Request(string Name, string Description, TransactionType Type);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("financial/categories/{id}", Handler)
            .WithTags(Tags.Financial)
            .RequireAuthorization();
    }

    private static async Task<IResult> Handler(Guid id, Request request, ISender sender, CancellationToken cancellationToken)
    {
        var command = new UpdateCategoryCommand(id, request.Name, request.Description, request.Type);

        Result result = await sender.Send(command, cancellationToken);

        return result.Match(Results.NoContent, CustomResults.Problem);
    }
}

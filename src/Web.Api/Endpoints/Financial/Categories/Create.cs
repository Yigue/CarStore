using Application.Financial.Categories.Create;
using Domain.Financial.Attributes;
using MediatR;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Financial.Categories;

public sealed class Create : IEndpoint
{
    public sealed record Request(string Name, string Description, TransactionType Type);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("financial/categories", Handler)
            .WithTags(Tags.Financial)
            .RequireAuthorization();
    }

    private static async Task<IResult> Handler(Request request, ISender sender, CancellationToken cancellationToken)
    {
        var command = new CreateCategoryCommand(request.Name, request.Description, request.Type);

        Result<Guid> result = await sender.Send(command, cancellationToken);

        return result.Match(Results.Ok, CustomResults.Problem);
    }
}

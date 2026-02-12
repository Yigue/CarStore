using Application.Financial.Categories.Get;
using MediatR;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Financial.Categories;

public sealed class Get : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("financial/categories", Handler)
            .WithTags(Tags.Financial)
            .RequireAuthorization();
    }

    private static async Task<IResult> Handler(ISender sender, CancellationToken cancellationToken)
    {
        Result<List<Domain.Financial.Attributes.TransactionCategory>> result = await sender.Send(new GetCategoriesQuery(), cancellationToken);

        return result.Match(Results.Ok, CustomResults.Problem);
    }
}

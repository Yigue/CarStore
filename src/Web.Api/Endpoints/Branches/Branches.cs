using Application.Branches;
using MediatR;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Branches;

/// <summary>sfc C2.1 / RN-048: the dealer's branches.</summary>
internal sealed class Branches : IEndpoint
{
    public sealed record Request(string Name);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("branches", async (ISender sender, CancellationToken cancellationToken) =>
        {
            Result<IReadOnlyList<BranchResponse>> result = await sender.Send(new GetBranchesQuery(), cancellationToken);
            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission("cars:read")
        .WithTags(Tags.DealerSettings)
        .WithName("GetBranches");

        app.MapPost("branches", async (Request request, ISender sender, CancellationToken cancellationToken) =>
        {
            Result<Guid> result = await sender.Send(new CreateBranchCommand(request.Name), cancellationToken);
            return result.Match(id => Results.Created($"/branches/{id}", new { id }), CustomResults.Problem);
        })
        .HasPermission("CanManageSettings")
        .WithTags(Tags.DealerSettings)
        .WithName("CreateBranch");
    }
}

using Application.Users.Queries.GetAgents;
using MediatR;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Users.GetAgents;

/// <summary>
/// Lightweight staff directory for assignment purposes (e.g. the "Vendedor" select on
/// SaleForm). Gated by sales:read instead of CanManageUsers so that non-admin staff
/// don't get a 403 (and an empty select) just for opening the sale form. Registered as
/// "users/agents": ASP.NET Core routing prefers the literal "agents" segment over the
/// parameterized users/{userId} route (see also the pre-existing users/me route, which
/// coexists with users/{userId} the same way), so there is no conflict with GetById.
/// </summary>
internal sealed class GetAgents : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("users/agents", async (ISender sender, CancellationToken cancellationToken) =>
        {
            var query = new GetAgentsQuery();

            Result<List<AgentResponse>> result = await sender.Send(query, cancellationToken);

            return result.Match(
                agents => Results.Ok(agents),
                CustomResults.Problem);
        })
        .HasPermission(Web.Api.Endpoints.Sales.Permissions.SalesRead)
        .WithTags(Tags.Users)
        .WithName("GetAgents")
        .Produces<List<AgentResponse>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}

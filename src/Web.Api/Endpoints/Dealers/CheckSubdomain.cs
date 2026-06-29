using Application.Dealers.CheckSubdomain;
using MediatR;
using Microsoft.Net.Http.Headers;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Dealers;

/// <summary>
/// Anonymous <c>GET /dealers/check-subdomain?subdomain={slug}</c> — fast UX
/// hint for the onboarding wizard. The DB unique index is the source of truth;
/// this endpoint is best-effort and may race with concurrent provisioning
/// (the loser sees <c>409 Conflict</c> from <see cref="Provision"/>).
/// Sends <c>Cache-Control: no-store</c> so the browser never caches stale results.
/// </summary>
internal sealed class CheckSubdomain : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("dealers/check-subdomain", async (
            string? subdomain,
            ISender sender,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(subdomain))
            {
                return Results.Problem(
                    title: "Dealers.InvalidSubdomain",
                    detail: "The 'subdomain' query parameter is required.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            var query = new CheckSubdomainAvailabilityQuery(subdomain);
            Result<SubdomainAvailabilityResponse> result = await sender.Send(query, cancellationToken);

            httpContext.Response.Headers[HeaderNames.CacheControl] = "no-store";

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.Dealers)
        .WithName("CheckSubdomainAvailability")
        .AllowAnonymous()
        .Produces<SubdomainAvailabilityResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
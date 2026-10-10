using Application.Dealers.Provision;
using MediatR;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Dealers;

/// <summary>
/// Anonymous <c>POST /dealers/provision</c> — atomically creates a new
/// dealer tenant (<c>DealerSettings</c> row) and its first Admin user.
/// The two writes run in a single EF Core transaction; the unique index on
/// <c>HostName</c> is the source of truth so concurrent submissions cannot
/// both succeed.
/// </summary>
internal sealed class Provision : IEndpoint
{
    public sealed record Request(
        string DealerName,
        string Subdomain,
        string AdminEmail,
        string AdminPassword,
        string AdminFirstName,
        string AdminLastName,
        string? AcceptedTermsVersion = null);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("dealers/provision", async (
            Request request,
            HttpContext httpContext,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var command = new ProvisionDealerCommand(
                DealerName: request.DealerName,
                Subdomain: request.Subdomain,
                AdminEmail: request.AdminEmail,
                AdminPassword: request.AdminPassword,
                AdminFirstName: request.AdminFirstName,
                AdminLastName: request.AdminLastName,
                AcceptedTermsVersion: request.AcceptedTermsVersion,
                AcceptanceIp: httpContext.Connection.RemoteIpAddress?.ToString(),
                AcceptanceUserAgent: httpContext.Request.Headers.UserAgent.ToString());

            Result<ProvisionDealerResponse> result = await sender.Send(command, cancellationToken);

            return result.Match(
                response => Results.Created(
                    $"/dealers/{response.DealerId}",
                    response),
                CustomResults.Problem);
        })
        .WithTags(Tags.Dealers)
        .WithName("ProvisionDealer")
        .AllowAnonymous()
        .RequireRateLimiting("public")
        .Produces<ProvisionDealerResponse>(StatusCodes.Status201Created)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status429TooManyRequests)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
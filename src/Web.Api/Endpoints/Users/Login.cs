using Application.Users.Login;
using MediatR;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Users;

internal sealed class Login : IEndpoint
{
    public sealed record Request(string Email, string Password);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("users/login", async (Request request, ISender sender, CancellationToken cancellationToken) =>
        {
            var command = new LoginUserCommand(request.Email, request.Password);

            Result<LoginResult> result = await sender.Send(command, cancellationToken);

            // sfc B6.3 / RN-043: single-dealer users keep the exact { token } shape as before this PR
            // (regression-critical). A 2+ dealer user instead gets a pending token + the dealer list.
            return result.Match(
                loginResult => loginResult.Token is not null
                    ? Results.Ok(new { token = loginResult.Token })
                    : Results.Ok(new
                    {
                        pendingToken = loginResult.PendingToken,
                        availableDealers = loginResult.AvailableDealers
                    }),
                CustomResults.Problem);
        })
        .WithTags(Tags.Users)
        .WithName("LoginUser")
        .AllowAnonymous()
        .RequireRateLimiting("login")
        .Produces<object>(StatusCodes.Status200OK)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status429TooManyRequests)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}

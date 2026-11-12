using Application.Marketing.Newsletter;
using MediatR;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Newsletter;

internal sealed class Subscribe : IEndpoint
{
    public sealed record Request(string Email);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        // sfc B5.1 / RN-019: creates a pending Subscriber (no longer a placeholder Client) and emails the double
        // opt-in link. The answer is the same whether or not the address was already subscribed.
        app.MapPost("newsletter/subscribe", async (
            Request request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            Result result = await sender.Send(new SubscribeCommand(request.Email ?? string.Empty), cancellationToken);

            return result.Match(
                () => Results.Ok(new { success = true, message = "Revisá tu correo para confirmar la suscripción." }),
                CustomResults.Problem);
        })
        .AllowAnonymous()
        .RequireRateLimiting("public")
        .WithTags("Newsletter")
        .WithName("SubscribeNewsletter")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}

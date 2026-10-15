using Application.Marketing.Newsletter.Tokens;
using MediatR;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Newsletter;

/// <summary>
/// sfc B5.1: the two emailed links. Unsubscribe answers GET (the page) and POST (RFC 8058 one-click, sent by the mail
/// client with a <c>List-Unsubscribe=One-Click</c> body that carries nothing the request needs).
/// </summary>
internal sealed class SubscriptionLinks : IEndpoint
{
    public sealed record ConfirmRequest(string Token);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("newsletter/confirm", async (ConfirmRequest request, ISender sender, CancellationToken cancellationToken) =>
        {
            Result result = await sender.Send(new ConfirmSubscriptionCommand(request.Token ?? string.Empty), cancellationToken);
            return result.Match(() => Results.Ok(new { success = true }), CustomResults.Problem);
        })
        .AllowAnonymous()
        .RequireRateLimiting("public")
        .WithTags("Newsletter")
        .WithName("ConfirmNewsletterSubscription")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        app.MapMethods("newsletter/unsubscribe", ["GET", "POST"], async (string? t, ISender sender, CancellationToken cancellationToken) =>
        {
            Result result = await sender.Send(new UnsubscribeCommand(t ?? string.Empty), cancellationToken);
            return result.Match(() => Results.Ok(new { success = true }), CustomResults.Problem);
        })
        .AllowAnonymous()
        .RequireRateLimiting("public")
        .WithTags("Newsletter")
        .WithName("UnsubscribeNewsletter")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest);
    }
}

using Application.Marketing.Newsletter.Tokens;
using MediatR;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Newsletter;

/// <summary>
/// sfc B5.1: the two emailed links. Both act only on POST: mail scanners and link prefetchers issue GETs, so a GET must
/// never confirm or unsubscribe (RFC 8058). The pages behind the links POST on an explicit click, and the mail client
/// POSTs the one-click unsubscribe with a <c>List-Unsubscribe=One-Click</c> body that carries nothing the request needs.
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

        app.MapPost("newsletter/unsubscribe", async (string? t, ISender sender, CancellationToken cancellationToken) =>
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

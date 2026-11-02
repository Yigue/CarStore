using System.Text.Json.Serialization;
using Infrastructure.Integrations.MercadoLibre;
using Microsoft.Extensions.Logging;
using SharedKernel;

namespace Web.Api.Endpoints.Integrations;

/// <summary>sfc C1.3 / RN-047: MercadoLibre's documented notification shape — snake_case, no signature.</summary>
public sealed record MercadoLibreWebhookRequest
{
    [JsonPropertyName("resource")]
    public string Resource { get; init; } = string.Empty;

    [JsonPropertyName("topic")]
    public string Topic { get; init; } = string.Empty;

    [JsonPropertyName("user_id")]
    public long UserId { get; init; }
}

/// <summary>
/// sfc C1.3 / ADR C25, RN-047: MercadoLibre never signs its webhook notifications — see
/// <see cref="MercadoLibreWebhookProcessor"/> for the pull-to-confirm verification this endpoint
/// delegates to. Always acks 200 so MercadoLibre's own aggressive retry policy does not pile up
/// redeliveries over a transient pull failure or a deliberately rejected purchase intent.
/// </summary>
internal sealed class MercadoLibreWebhook : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("integrations/mercadolibre/webhook", async (
            MercadoLibreWebhookRequest request,
            MercadoLibreWebhookProcessor processor,
            ILogger<MercadoLibreWebhook> logger,
            CancellationToken cancellationToken) =>
        {
            Result result = await processor.ProcessAsync(
                new MercadoLibreNotification(request.Topic, request.Resource, request.UserId),
                cancellationToken);

            if (result.IsFailure)
            {
                logger.LogInformation(
                    "MercadoLibre webhook for resource {Resource} was not applied: {Error}", request.Resource, result.Error.Description);
            }

            return Results.Ok();
        })
        .AllowAnonymous()
        .WithTags("Integrations")
        .WithName("MercadoLibreWebhook")
        .Produces(StatusCodes.Status200OK);
    }
}

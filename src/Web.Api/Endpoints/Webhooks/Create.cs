using Application.Webhooks.Create;
using MediatR;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Webhooks;

internal sealed class Create : IEndpoint
{
    public sealed record Request(string Url, IReadOnlyList<string> EventTypes);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("webhooks", async (Request request, ISender sender, CancellationToken ct) =>
        {
            var command = new CreateWebhookSubscriptionCommand(request.Url, request.EventTypes);
            Result<CreateWebhookSubscriptionResponse> result = await sender.Send(command, ct);

            return result.Match(
                response => Results.Created($"/webhooks/{response.Id}", response),
                CustomResults.Problem);
        })
        .HasPermission(Permissions.WebhooksManage)
        .WithTags(Tags.Webhooks)
        .WithName("CreateWebhookSubscription")
        .Produces<CreateWebhookSubscriptionResponse>(StatusCodes.Status201Created)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}

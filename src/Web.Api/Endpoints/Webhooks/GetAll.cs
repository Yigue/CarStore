using Application.Webhooks.GetAll;
using MediatR;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Webhooks;

internal sealed class GetAll : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("webhooks", async (ISender sender, CancellationToken ct) =>
        {
            var query = new GetWebhookSubscriptionsQuery();
            Result<List<WebhookSubscriptionResponse>> result = await sender.Send(query, ct);

            return result.Match(
                subscriptions => Results.Ok(subscriptions),
                CustomResults.Problem);
        })
        .HasPermission(Permissions.WebhooksManage)
        .WithTags(Tags.Webhooks)
        .WithName("GetAllWebhookSubscriptions")
        .Produces<List<WebhookSubscriptionResponse>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}

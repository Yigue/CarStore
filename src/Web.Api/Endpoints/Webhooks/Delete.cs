using Application.Webhooks.Delete;
using MediatR;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Webhooks;

internal sealed class Delete : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapDelete("webhooks/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var command = new DeleteWebhookSubscriptionCommand(id);
            Result result = await sender.Send(command, ct);

            return result.Match(
                () => Results.NoContent(),
                CustomResults.Problem);
        })
        .HasPermission(Permissions.WebhooksManage)
        .WithTags(Tags.Webhooks)
        .WithName("DeleteWebhookSubscription")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}

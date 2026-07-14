using Application.Webhooks.Update;
using MediatR;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Webhooks;

internal sealed class Update : IEndpoint
{
    public sealed record Request(string Url, IReadOnlyList<string> EventTypes, bool IsActive);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("webhooks/{id:guid}", async (Guid id, Request request, ISender sender, CancellationToken ct) =>
        {
            var command = new UpdateWebhookSubscriptionCommand(id, request.Url, request.EventTypes, request.IsActive);
            Result result = await sender.Send(command, ct);

            return result.Match(
                () => Results.NoContent(),
                CustomResults.Problem);
        })
        .HasPermission(Permissions.WebhooksManage)
        .WithTags(Tags.Webhooks)
        .WithName("UpdateWebhookSubscription")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}

using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Webhooks;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Webhooks.Delete;

internal sealed class DeleteWebhookSubscriptionCommandHandler(IApplicationDbContext context)
    : ICommandHandler<DeleteWebhookSubscriptionCommand>
{
    public async Task<Result> Handle(DeleteWebhookSubscriptionCommand command, CancellationToken cancellationToken)
    {
        WebhookSubscription? subscription = await context.WebhookSubscriptions
            .FirstOrDefaultAsync(s => s.Id == command.Id, cancellationToken);

        if (subscription is null)
        {
            return Result.Failure(WebhookErrors.NotFound(command.Id));
        }

        context.WebhookSubscriptions.Remove(subscription);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

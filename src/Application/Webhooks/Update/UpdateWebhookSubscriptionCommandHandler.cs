using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Webhooks;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Webhooks.Update;

internal sealed class UpdateWebhookSubscriptionCommandHandler(IApplicationDbContext context)
    : ICommandHandler<UpdateWebhookSubscriptionCommand>
{
    public async Task<Result> Handle(UpdateWebhookSubscriptionCommand command, CancellationToken cancellationToken)
    {
        WebhookSubscription? subscription = await context.WebhookSubscriptions
            .FirstOrDefaultAsync(s => s.Id == command.Id, cancellationToken);

        if (subscription is null)
        {
            return Result.Failure(WebhookErrors.NotFound(command.Id));
        }

        try
        {
            subscription.UpdateDetails(command.Url, command.EventTypes, command.IsActive);
            await context.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (DomainException ex)
        {
            return Result.Failure(Error.Validation("Webhooks.DomainError", ex.Message));
        }
    }
}

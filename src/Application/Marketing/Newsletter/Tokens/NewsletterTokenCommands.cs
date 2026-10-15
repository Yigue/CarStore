using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Users.Verification;
using Domain.Marketing;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Marketing.Newsletter.Tokens;

/// <summary>sfc B5.1: the double opt-in link.</summary>
public sealed record ConfirmSubscriptionCommand(string Token) : ICommand;

/// <summary>sfc B5.1 / RFC 8058: the one-click unsubscribe link (GET from the page, POST from the mail client).</summary>
public sealed record UnsubscribeCommand(string Token) : ICommand;

// The links are opened anonymously and, for RFC 8058 one-click, POSTed by the mail client with no dealer headers,
// so the lookup bypasses the tenant filter: the 256-bit token, looked up by its SHA-256 digest, is the identity.
internal sealed class ConfirmSubscriptionCommandHandler(IApplicationDbContext context, IDateTimeProvider dateTimeProvider)
    : ICommandHandler<ConfirmSubscriptionCommand>
{
    public async Task<Result> Handle(ConfirmSubscriptionCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Token))
        {
            return Result.Failure(SubscriberErrors.InvalidToken);
        }

        string hash = UserActionTokenHasher.Hash(command.Token);
        Subscriber? subscriber = await context.Subscribers.IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.ConfirmTokenHash == hash, cancellationToken);
        if (subscriber is null || !subscriber.Confirm(dateTimeProvider.UtcNow))
        {
            return Result.Failure(SubscriberErrors.InvalidToken);
        }

        await context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

internal sealed class UnsubscribeCommandHandler(IApplicationDbContext context, IDateTimeProvider dateTimeProvider)
    : ICommandHandler<UnsubscribeCommand>
{
    public async Task<Result> Handle(UnsubscribeCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Token))
        {
            return Result.Failure(SubscriberErrors.InvalidToken);
        }

        string hash = UserActionTokenHasher.Hash(command.Token);
        Subscriber? subscriber = await context.Subscribers.IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.UnsubscribeTokenHash == hash, cancellationToken);
        if (subscriber is null)
        {
            return Result.Failure(SubscriberErrors.InvalidToken);
        }

        // Idempotent: a second click on the same link succeeds and changes nothing.
        subscriber.Unsubscribe(dateTimeProvider.UtcNow);
        await context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

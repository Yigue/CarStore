using Application.Abstractions.Data;
using Application.Abstractions.Email;
using Application.Abstractions.Messaging;
using Application.Abstractions.Tenancy;
using Application.Users.Verification;
using Domain.Marketing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SharedKernel;

namespace Application.Marketing.Newsletter;

/// <summary>sfc B5.1 / RN-019: public newsletter sign-up for the dealer resolved from the request.</summary>
public sealed record SubscribeCommand(string Email, string Source = "web") : ICommand;

/// <summary>
/// Creates a pending <see cref="Subscriber"/> and emails the double opt-in link. Every outcome for a well-formed
/// address answers the same (new, pending, confirmed, a lost race, a failed email), so the endpoint does not reveal
/// who is subscribed. A confirmed address gets no email. The email is queued, not sent here, so the response time
/// does not reveal it either; the remaining difference is one database write (residual risk, as in ResendVerification).
/// </summary>
internal sealed class SubscribeCommandHandler(
    IApplicationDbContext context,
    ICurrentTenantService tenantService,
    IDateTimeProvider dateTimeProvider,
    IBackgroundEmailQueue emailQueue,
    IConfiguration configuration,
    ILogger<SubscribeCommandHandler> logger)
    : ICommandHandler<SubscribeCommand>
{
    public async Task<Result> Handle(SubscribeCommand command, CancellationToken cancellationToken)
    {
        if (!tenantService.HasTenant)
        {
            return Result.Failure(Error.Validation("Newsletter.DealerRequired", "No se pudo identificar la concesionaria."));
        }

        DateTime now = dateTimeProvider.UtcNow;
        string confirmSecret = UserActionTokenHasher.NewToken();
        string unsubscribeSecret = UserActionTokenHasher.NewToken();

        Result<Subscriber> created = Subscriber.Subscribe(
            tenantService.DealerId, command.Email, command.Source,
            UserActionTokenHasher.Hash(confirmSecret), UserActionTokenHasher.Hash(unsubscribeSecret), now);
        if (created.IsFailure)
        {
            return Result.Failure(created.Error);
        }

        Subscriber candidate = created.Value;
        Subscriber? existing = await context.Subscribers.FirstOrDefaultAsync(
            s => s.DealerId == tenantService.DealerId && s.EmailNormalized == candidate.EmailNormalized, cancellationToken);

        Subscriber subscriber;
        if (existing is null)
        {
            context.Subscribers.Add(candidate);
            subscriber = candidate;
        }
        else
        {
            if (!existing.Resubscribe(UserActionTokenHasher.Hash(confirmSecret), now))
            {
                // Same calls as the other paths; with nothing tracked this is a no-op.
                await context.SaveChangesAsync(cancellationToken);
                return Result.Success();
            }

            // Only the digest of the old unsubscribe secret is stored, so the new email carries a new one.
            existing.RotateUnsubscribeToken(UserActionTokenHasher.Hash(unsubscribeSecret));
            subscriber = existing;
        }

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // A concurrent request created the same (dealer, email) row first; answer like any duplicate.
            return Result.Success();
        }

        string baseUrl = (configuration["Frontend:BaseUrl"] ?? "https://carstore-app.com").TrimEnd('/');
        bool queued = emailQueue.TryEnqueue(
            EmailTemplate.NewsletterConfirm,
            subscriber.Email,
            new Dictionary<string, string>
            {
                ["ConfirmUrl"] = $"{baseUrl}/newsletter/confirmar?t={confirmSecret}",
                ["UnsubscribeUrl"] = $"{baseUrl}/newsletter/baja?t={unsubscribeSecret}",
                ["ExpiresInDays"] = Subscriber.ConfirmationLifetime.TotalDays.ToString("0")
            });
        if (!queued)
        {
            // The answer must not depend on it; the person can sign up again to get a new link.
            logger.LogWarning("Email queue full: newsletter confirmation for subscriber {SubscriberId} dropped", subscriber.Id);
        }

        return Result.Success();
    }
}

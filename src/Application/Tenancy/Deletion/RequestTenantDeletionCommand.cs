using Application.Abstractions.Authentication;
using Application.Abstractions.Billing;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Tenancy;
using Domain.Billing;
using Domain.DealerSettings;
using Domain.Users;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SharedKernel;
using DealerSettingsEntity = Domain.DealerSettings.DealerSettings;

namespace Application.Tenancy.Deletion;

/// <summary>
/// RN-015 (A23): the owner asks to delete the dealer account. The confirmation is the password (re-authentication)
/// plus the dealer slug typed by hand. Nothing is deleted here: the subscription is cancelled and the tenant enters
/// the same 30 day grace / 90 day purge flow as a cancellation (RN-013).
/// </summary>
public sealed record RequestTenantDeletionCommand(string Password, string ConfirmationSlug) : ICommand<TenantDeletionResult>;

public sealed record TenantDeletionResult(DateTime CancelledAt, DateTime PurgeScheduledAt);

public static class TenantDeletionErrors
{
    public static readonly Error NotFound = new(
        "TenantDeletion.NotFound", "No se encontró la cuenta.", ErrorType.NotFound);

    public static readonly Error NotOwner = new(
        "TenantDeletion.NotOwner", "Solo el titular de la cuenta puede solicitar su eliminación.", ErrorType.Forbidden);

    public static readonly Error ReauthenticationFailed = new(
        "TenantDeletion.ReauthenticationFailed", "La contraseña no es correcta.", ErrorType.Forbidden);

    public static readonly Error ConfirmationMismatch = new(
        "TenantDeletion.ConfirmationMismatch", "El texto de confirmación no coincide.", ErrorType.Validation);

    public static readonly Error AlreadyInProgress = new(
        "TenantDeletion.AlreadyInProgress", "La cuenta ya está en proceso de baja.", ErrorType.Conflict);
}

internal sealed class RequestTenantDeletionCommandValidator : AbstractValidator<RequestTenantDeletionCommand>
{
    public RequestTenantDeletionCommandValidator()
    {
        RuleFor(x => x.Password).NotEmpty();
        RuleFor(x => x.ConfirmationSlug).NotEmpty().WithMessage("El texto de confirmación no coincide");
    }
}

internal sealed class RequestTenantDeletionCommandHandler(
    IApplicationDbContext context,
    ICurrentTenantService tenant,
    IUserContext userContext,
    IPasswordHasher passwordHasher,
    ISubscriptionGateway gateway,
    ITenantLifecycleCache lifecycleCache,
    IDateTimeProvider clock,
    IConfiguration configuration) : ICommandHandler<RequestTenantDeletionCommand, TenantDeletionResult>
{
    private const string AdminRoleName = "Admin";

    public async Task<Result<TenantDeletionResult>> Handle(RequestTenantDeletionCommand request, CancellationToken ct)
    {
        Guid dealerId = tenant.DealerId;

        DealerSettingsEntity? dealer = await context.DealerSettings
            .FirstOrDefaultAsync(d => d.DealerId == dealerId, ct);
        if (dealer is null)
            return Result.Failure<TenantDeletionResult>(TenantDeletionErrors.NotFound);

        User? user = await context.Users
            .FirstOrDefaultAsync(u => u.Id == userContext.UserId && u.DealerId == dealerId, ct);
        if (user is null)
            return Result.Failure<TenantDeletionResult>(TenantDeletionErrors.ReauthenticationFailed);

        // There is no explicit owner column yet (sfc C17): the dealer administrator is the owner. The role is read
        // from the database, not from the JWT "role" claim: with inbound claim mapping on, IUserContext.IsAdmin
        // (FindFirst("role")) is false for real requests.
        bool isOwner = await context.Roles
            .AnyAsync(r => r.Id == user.RoleId && r.DealerId == dealerId && r.Name == AdminRoleName, ct);
        if (!isOwner)
            return Result.Failure<TenantDeletionResult>(TenantDeletionErrors.NotOwner);

        if (!passwordHasher.Verify(request.Password ?? string.Empty, user.PasswordHash))
            return Result.Failure<TenantDeletionResult>(TenantDeletionErrors.ReauthenticationFailed);

        // Typed by hand, exact match: no trimming, no case folding.
        if (string.IsNullOrEmpty(dealer.Slug)
            || !string.Equals(request.ConfirmationSlug, dealer.Slug, StringComparison.Ordinal))
            return Result.Failure<TenantDeletionResult>(TenantDeletionErrors.ConfirmationMismatch);

        if (dealer.LifecycleStatus != TenantLifecycleStatus.Active)
            return Result.Failure<TenantDeletionResult>(TenantDeletionErrors.AlreadyInProgress);

        DateTime now = clock.UtcNow;

        // Provider first: if it fails nothing is persisted and the owner can retry.
        List<DealerSubscription> subscriptions = await context.DealerSubscriptions
            .Where(s => s.DealerId == dealerId && s.Status != SubscriptionStatus.Cancelled)
            .ToListAsync(ct);
        foreach (DealerSubscription subscription in subscriptions)
        {
            if (!string.IsNullOrWhiteSpace(subscription.StripeSubscriptionId))
                await gateway.CancelNowAsync(subscription.StripeSubscriptionId, ct);

            subscription.Cancel();
        }

        int days = int.TryParse(configuration[StartTenantRetentionHandler.PurgeAfterDaysKey], out int configured) && configured > 0
            ? configured
            : StartTenantRetentionHandler.DefaultPurgeAfterDays;

        // Same transition the SubscriptionCancelled event handler applies (idempotent), done now so the response is accurate.
        dealer.StartRetention(now, days);
        await context.SaveChangesAsync(ct);
        lifecycleCache.Invalidate(dealerId);

        return new TenantDeletionResult(now, dealer.PurgeScheduledAt ?? now.AddDays(days));
    }
}

using Application.Abstractions.Data;
using Application.Abstractions.Email;
using Application.Abstractions.Messaging;
using Domain.Shared.ValueObjects;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SharedKernel;

namespace Application.Users.Verification.ResendVerification;

/// <summary>
/// Issues a fresh verification token and emails it. The result is always success, whether the account exists,
/// is already verified, or the email transport failed, so the endpoint cannot be used to enumerate accounts.
/// </summary>
internal sealed class ResendVerificationCommandHandler(
    IApplicationDbContext context,
    ITransactionalEmailSender emailSender,
    IConfiguration configuration,
    ILogger<ResendVerificationCommandHandler> logger)
    : ICommandHandler<ResendVerificationCommand>
{
    public async Task<Result> Handle(ResendVerificationCommand command, CancellationToken cancellationToken)
    {
        Email email;
        try
        {
            email = new Email(command.Email ?? string.Empty);
        }
        catch (DomainException)
        {
            // A malformed address cannot match an account; answer like any other unknown address.
            return Result.Success();
        }

        User? user = await context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => (string)u.Email == email.Value && u.IsActive, cancellationToken);

        if (user is null || user.IsEmailVerified || user.IsPlatformAdmin)
        {
            return Result.Success();
        }

        DateTime now = DateTime.UtcNow;
        string address = user.Email.Value;

        List<UserActionToken> pending = await context.UserActionTokens
            .Where(t => t.Purpose == UserActionPurpose.EmailVerification && t.ConsumedAtUtc == null
                && t.Email == address)
            .ToListAsync(cancellationToken);

        string secret = UserActionTokenHasher.NewToken();
        context.UserActionTokens.Add(UserActionToken.Issue(
            pending, UserActionPurpose.EmailVerification, user.DealerId, address,
            UserActionTokenHasher.Hash(secret), user.Id, null, null, now));
        await context.SaveChangesAsync(cancellationToken);

        string baseUrl = (configuration["Frontend:BaseUrl"] ?? "https://carstore-app.com").TrimEnd('/');
        try
        {
            await emailSender.SendAsync(
                EmailTemplate.VerifyEmail,
                address,
                new Dictionary<string, string>
                {
                    ["Name"] = user.FirstName,
                    ["VerifyUrl"] = $"{baseUrl}/verificar?token={secret}",
                    ["ExpiresInHours"] = UserActionToken.LifetimeOf(UserActionPurpose.EmailVerification).TotalHours.ToString("0")
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            // Swallowed on purpose: surfacing a transport error would reveal that the account exists.
            logger.LogError(ex, "Could not send the verification email for user {UserId}", user.Id);
        }

        return Result.Success();
    }
}

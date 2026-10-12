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

        // Timing side channel (sfc A4.1): every branch below does the same lookups, the same token generation and
        // hashing, and the same SaveChanges call, so the handler's cost does not reveal whether the account exists.
        // The email send is not equalized and stays slower for real accounts (see residual risk in the change notes).
        bool canIssue = user is not null && !user.IsEmailVerified && !user.IsPlatformAdmin;
        DateTime now = DateTime.UtcNow;
        string address = user?.Email.Value ?? email.Value;

        DbSet<UserActionToken> tokens = context.UserActionTokens;
        List<UserActionToken> pending = await tokens
            .Where(t => t.Purpose == UserActionPurpose.EmailVerification && t.ConsumedAtUtc == null
                && t.Email == address)
            .ToListAsync(cancellationToken);

        string secret = UserActionTokenHasher.NewToken();
        string secretHash = UserActionTokenHasher.Hash(secret);
        if (canIssue)
        {
            tokens.Add(UserActionToken.Issue(
                pending, UserActionPurpose.EmailVerification, user!.DealerId, address,
                secretHash, user.Id, null, null, now));
        }

        // With nothing tracked this is a no-op for unknown or non-verifiable accounts; it is still called on every path.
        await context.SaveChangesAsync(cancellationToken);

        if (!canIssue)
        {
            return Result.Success();
        }

        string baseUrl = (configuration["Frontend:BaseUrl"] ?? "https://carstore-app.com").TrimEnd('/');
        try
        {
            await emailSender.SendAsync(
                EmailTemplate.VerifyEmail,
                address,
                new Dictionary<string, string>
                {
                    ["Name"] = user!.FirstName,
                    ["VerifyUrl"] = $"{baseUrl}/verificar?token={secret}",
                    ["ExpiresInHours"] = UserActionToken.LifetimeOf(UserActionPurpose.EmailVerification).TotalHours.ToString("0")
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            // Swallowed on purpose: surfacing a transport error would reveal that the account exists.
            logger.LogError(ex, "Could not send the verification email for user {UserId}", user!.Id);
        }

        return Result.Success();
    }
}

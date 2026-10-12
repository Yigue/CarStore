using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Users.Verification.VerifyEmail;

internal sealed class VerifyEmailCommandHandler(IApplicationDbContext context)
    : ICommandHandler<VerifyEmailCommand>
{
    public async Task<Result> Handle(VerifyEmailCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Token))
        {
            return Result.Failure(UserErrors.InvalidVerificationToken);
        }

        DateTime now = DateTime.UtcNow;
        string tokenHash = UserActionTokenHasher.Hash(command.Token);

        // Anonymous request: the token may belong to any dealer, so there is no tenant filter on tokens.
        UserActionToken? token = await context.UserActionTokens
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash && t.Purpose == UserActionPurpose.EmailVerification, cancellationToken);

        if (token is null || token.UserId is null || !token.IsPending(now))
        {
            return Result.Failure(UserErrors.InvalidVerificationToken);
        }

        // Atomic single use: of N concurrent requests exactly one gets rowcount = 1.
        if (!await context.TryConsumeUserActionTokenAsync(token.Id, now, cancellationToken))
        {
            return Result.Failure(UserErrors.InvalidVerificationToken);
        }

        User? user = await context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == token.UserId, cancellationToken);

        if (user is null)
        {
            return Result.Failure(UserErrors.InvalidVerificationToken);
        }

        user.MarkEmailVerified(now);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

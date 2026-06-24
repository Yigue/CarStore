using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Users.ResetPassword;

internal sealed class ResetPasswordCommandHandler(
    IApplicationDbContext context,
    IPasswordHasher passwordHasher)
    : ICommandHandler<ResetPasswordCommand>
{
    public async Task<Result> Handle(ResetPasswordCommand command, CancellationToken cancellationToken)
    {
        DateTime now = DateTime.UtcNow;

        PasswordResetToken? resetToken = await context.PasswordResetTokens
            .FirstOrDefaultAsync(t => t.Token == command.Token, cancellationToken);

        if (resetToken is null || !resetToken.IsUsable(now))
        {
            return Result.Failure(UserErrors.InvalidResetToken);
        }

        // Anonymous request: the user may belong to any dealer, so bypass the tenant filter.
        User? user = await context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == resetToken.UserId, cancellationToken);

        if (user is null)
        {
            return Result.Failure(UserErrors.InvalidResetToken);
        }

        user.SetPassword(passwordHasher.Hash(command.NewPassword));
        resetToken.MarkUsed(now);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

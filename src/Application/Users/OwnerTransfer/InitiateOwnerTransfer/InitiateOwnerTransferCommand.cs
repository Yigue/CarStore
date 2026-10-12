using Application.Abstractions.Messaging;
using FluentValidation;

namespace Application.Users.OwnerTransfer.InitiateOwnerTransfer;

/// <summary>sfc A4.3 follow-up: starting a transfer re-authenticates the owner with the password.</summary>
public sealed record InitiateOwnerTransferCommand(Guid TargetUserId, string Password) : ICommand;

internal sealed class InitiateOwnerTransferCommandValidator : AbstractValidator<InitiateOwnerTransferCommand>
{
    public InitiateOwnerTransferCommandValidator()
     {
        RuleFor(x => x.TargetUserId).NotEmpty();
        RuleFor(x => x.Password).NotEmpty();
    }
}

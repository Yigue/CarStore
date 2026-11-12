using Application.Abstractions.Messaging;
using FluentValidation;

namespace Application.Users.Invitations.InviteUser;

/// <summary>sfc A4.2 / A14 / RN-031: invites someone to join the current dealer under <c>RoleId</c>.</summary>
public sealed record InviteUserCommand(string Email, Guid RoleId) : ICommand;

internal sealed class InviteUserCommandValidator : AbstractValidator<InviteUserCommand>
{
    public InviteUserCommandValidator()
    {
        RuleFor(c => c.Email)
            .NotEmpty()
            .EmailAddress()
            .WithMessage("El email es requerido y debe ser válido");

        RuleFor(c => c.RoleId)
            .NotEmpty()
            .WithMessage("El rol debe ser válido");
    }
}

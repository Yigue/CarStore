using Application.Abstractions.Messaging;
using Application.Common;
using FluentValidation;

namespace Application.Users.Invitations.AcceptInvitation;

/// <summary>sfc A4.2 / A14 / RN-031: anonymous — the token (not a session) is the only identity proof.</summary>
public sealed record AcceptInvitationCommand(string Token, string Password, string FirstName, string LastName) : ICommand<Guid>;

internal sealed class AcceptInvitationCommandValidator : AbstractValidator<AcceptInvitationCommand>
{
    public AcceptInvitationCommandValidator()
    {
        RuleFor(c => c.Token).NotEmpty();

        RuleFor(c => c.Password).MustBeStrongPassword();

        RuleFor(c => c.FirstName)
            .NotEmpty()
            .MaximumLength(100)
            .WithMessage("El nombre es requerido");

        RuleFor(c => c.LastName)
            .NotEmpty()
            .MaximumLength(100)
            .WithMessage("El apellido es requerido");
    }
}

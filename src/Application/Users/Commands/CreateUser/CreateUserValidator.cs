using Application.Common;
using FluentValidation;

namespace Application.Users.Commands.CreateUser;

internal sealed class CreateUserValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserValidator()
    {
        RuleFor(c => c.Email)
            .NotEmpty()
            .EmailAddress()
            .WithMessage("El email es requerido y debe ser válido");

        RuleFor(c => c.Password).MustBeStrongPassword();

        RuleFor(c => c.FirstName)
            .NotEmpty()
            .MaximumLength(100)
            .WithMessage("El nombre es requerido");

        RuleFor(c => c.LastName)
            .NotEmpty()
            .MaximumLength(100)
            .WithMessage("El apellido es requerido");

        RuleFor(c => c.Phone)
            .MaximumLength(20)
            .WithMessage("El teléfono no puede exceder 20 caracteres")
            .When(c => c.Phone is not null);

        RuleFor(c => c.RoleId)
            .NotEmpty()
            .WithMessage("El rol debe ser válido");
    }
}
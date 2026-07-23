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

        RuleFor(c => c.Password)
            .NotEmpty()
            .MinimumLength(8)
                .WithMessage("La contraseña debe tener al menos 8 caracteres")
            .Matches("[A-Z]")
                .WithMessage("La contraseña debe contener al menos una mayúscula")
            .Matches("[a-z]")
                .WithMessage("La contraseña debe contener al menos una minúscula")
            .Matches("[0-9]")
                .WithMessage("La contraseña debe contener al menos un número")
            .Matches(@"[@$!%*?&]")
                .WithMessage("La contraseña debe contener al menos un carácter especial (@$!%*?&)");

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
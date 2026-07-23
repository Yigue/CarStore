using FluentValidation;

namespace Application.Users.Commands.UpdateUser;

internal sealed class UpdateUserValidator : AbstractValidator<UpdateUserCommand>
{
    public UpdateUserValidator()
    {
        RuleFor(c => c.UserId)
            .NotEmpty()
            .WithMessage("El ID de usuario es requerido");

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
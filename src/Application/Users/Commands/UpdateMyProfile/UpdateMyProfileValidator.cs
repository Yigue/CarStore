using FluentValidation;

namespace Application.Users.Commands.UpdateMyProfile;

internal sealed class UpdateMyProfileValidator : AbstractValidator<UpdateMyProfileCommand>
{
    public UpdateMyProfileValidator()
    {
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
    }
}

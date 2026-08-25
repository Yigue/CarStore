using FluentValidation;

namespace Application.Users.ChangePassword;

public sealed class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(x => x.CurrentPassword)
            .NotEmpty()
            .WithMessage("Ingresá tu contraseña actual.");

        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .MinimumLength(8)
            .WithMessage("La contraseña debe tener al menos 8 caracteres.");

        // Un "cambio" que deja la misma contraseña no es un cambio, y aceptarlo
        // en silencio le hace creer al usuario que rotó su credencial.
        RuleFor(x => x.NewPassword)
            .NotEqual(x => x.CurrentPassword)
            .WithMessage("La contraseña nueva debe ser distinta de la actual.");
    }
}

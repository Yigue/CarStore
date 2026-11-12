using FluentValidation;

namespace Application.Common;

/// <summary>
/// The single password policy (CU-13): at least <see cref="MinimumLength"/> characters with an
/// uppercase letter, a lowercase letter and a digit. Register, create-user, reset-password and
/// change-password all go through here; the frontend schema mirrors it.
/// </summary>
public static class PasswordRules
{
    public const int MinimumLength = 10;

    public static IRuleBuilderOptions<T, string> MustBeStrongPassword<T>(this IRuleBuilder<T, string> rule) =>
        rule
            .NotEmpty()
                .WithMessage("La contraseña es requerida.")
            .MinimumLength(MinimumLength)
                .WithMessage($"La contraseña debe tener al menos {MinimumLength} caracteres.")
            .Matches("[A-Z]")
                .WithMessage("La contraseña debe contener al menos una mayúscula.")
            .Matches("[a-z]")
                .WithMessage("La contraseña debe contener al menos una minúscula.")
            .Matches("[0-9]")
                .WithMessage("La contraseña debe contener al menos un número.");
}

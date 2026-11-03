using FluentValidation;

namespace Application.Public.TestDrive;

/// <summary>Same posture as <c>CreateInquiryCommandValidator</c> — Phone optional, everything else required.</summary>
internal sealed class CreateTestDriveCommandValidator : AbstractValidator<CreateTestDriveCommand>
{
    public CreateTestDriveCommandValidator()
    {
        RuleFor(x => x.CarId).NotEmpty();

        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("El nombre es requerido")
            .MaximumLength(100).WithMessage("El nombre no puede superar los 100 caracteres");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("El apellido es requerido")
            .MaximumLength(100).WithMessage("El apellido no puede superar los 100 caracteres");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("El email es requerido")
            .EmailAddress().WithMessage("El email no tiene un formato valido")
            .When(x => !string.IsNullOrWhiteSpace(x.Email))
            .MaximumLength(256).WithMessage("El email no puede superar los 256 caracteres");

        RuleFor(x => x.Phone)
            .MaximumLength(50).WithMessage("El telefono no puede superar los 50 caracteres");

        RuleFor(x => x.Comments)
            .MaximumLength(2000).WithMessage("El comentario no puede superar los 2000 caracteres");

        // Honeypot: never shown to or filled in by a human, so no human-friendly message needed.
        RuleFor(x => x.Website)
            .MaximumLength(500);
    }
}

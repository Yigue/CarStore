using FluentValidation;

namespace Application.Leads.RegisterWhatsAppClickIntent;

internal sealed class RegisterWhatsAppClickIntentCommandValidator : AbstractValidator<RegisterWhatsAppClickIntentCommand>
{
    public RegisterWhatsAppClickIntentCommandValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("El código de referencia es requerido");
        RuleFor(x => x.Code)
            .MaximumLength(50).WithMessage("El código de referencia no puede superar los 50 caracteres");
    }
}

using FluentValidation;

namespace Application.Quotes.CreateInquiry;

/// <summary>
/// The public inquiry endpoint had no validator at all: name, surname and phone reached the
/// handler unchecked, and an empty name surfaced as a DomainException from deep inside an
/// aggregate — a 500 where the caller deserved a 400 naming the field.
///
/// Phone is intentionally optional and mirrors <see cref="Domain.Leads.Lead.Create"/>: all three
/// public forms treat it as optional, one of them labelling it "(Opcional)" to the visitor.
/// Requiring it here would reject the traffic this endpoint exists to capture.
/// </summary>
internal sealed class CreateInquiryCommandValidator : AbstractValidator<CreateInquiryCommand>
{
    public CreateInquiryCommandValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("El nombre es requerido");
        RuleFor(x => x.FirstName)
            .MaximumLength(100).WithMessage("El nombre no puede superar los 100 caracteres");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("El apellido es requerido");
        RuleFor(x => x.LastName)
            .MaximumLength(100).WithMessage("El apellido no puede superar los 100 caracteres");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("El email es requerido");
        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("El email no tiene un formato valido")
            .When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Email)
            .MaximumLength(256).WithMessage("El email no puede superar los 256 caracteres");

        // Optional — see the class summary.
        RuleFor(x => x.Phone)
            .MaximumLength(50).WithMessage("El telefono no puede superar los 50 caracteres");

        RuleFor(x => x.Comments)
            .MaximumLength(2000).WithMessage("La consulta no puede superar los 2000 caracteres");
    }
}

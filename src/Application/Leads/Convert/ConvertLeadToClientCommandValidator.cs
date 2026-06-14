using FluentValidation;

namespace Application.Leads.Convert;

public sealed class ConvertLeadToClientCommandValidator : AbstractValidator<ConvertLeadToClientCommand>
{
    public ConvertLeadToClientCommandValidator()
    {
        RuleFor(x => x.Dni)
            .NotEmpty().WithMessage("El DNI es obligatorio para convertir el lead en cliente.")
            .Must(dni => !string.IsNullOrWhiteSpace(dni))
            .WithMessage("El DNI no puede contener solo espacios en blanco.");
    }
}

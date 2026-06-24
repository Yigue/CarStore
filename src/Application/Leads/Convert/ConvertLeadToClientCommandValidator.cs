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

        RuleFor(x => x.Address)
            .NotEmpty().WithMessage("La dirección es obligatoria para convertir el lead en cliente.")
            .Must(address => !string.IsNullOrWhiteSpace(address))
            .WithMessage("La dirección no puede contener solo espacios en blanco.");
    }
}

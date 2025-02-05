using Domain.Cars;
using Domain.Clients;
using Domain.Sales;
using FluentValidation;

namespace Application.Financial.Create;

public sealed class CreateFinancialCommandValidator : AbstractValidator<CreateFinancialCommand>
{
    public CreateFinancialCommandValidator()
    {
        RuleFor(x => x.Type)
            .NotEmpty().WithMessage("El tipo de transacción es requerido")
            .IsInEnum().WithMessage("El tipo de transacción debe ser un valor válido");

        RuleFor(x => x.Amount)
            .NotEmpty().WithMessage("El monto es requerido")
            .GreaterThan(0).WithMessage("El monto debe ser mayor que cero");

        RuleFor(x => x.PaymentMethod)
            .IsInEnum().WithMessage("El método de pago debe ser un valor válido")
            .NotEmpty().WithMessage("El método de pago es requerido");

        RuleFor(x => x.CategoryId)
            .NotEmpty().WithMessage("La categoría es requerida");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("La descripción es requerida")
            .MaximumLength(255).WithMessage("La descripción no puede exceder los 255 caracteres");

       
    }
}

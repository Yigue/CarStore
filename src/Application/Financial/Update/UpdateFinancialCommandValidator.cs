using FluentValidation;
using Domain.Financial.Attributes;
using Domain.Cars;
using Domain.Clients;
using Domain.Sales;

namespace Application.Financial.Update;

public sealed class UpdateFinancialCommandValidator : AbstractValidator<UpdateFinancialCommand>
{
    public UpdateFinancialCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("El ID es requerido");

        RuleFor(x => x.Type)
            .NotEmpty().WithMessage("El tipo de transacción es requerido")
            .IsInEnum().WithMessage("El tipo de transacción debe ser un valor válido");

        RuleFor(x => x.Amount)
            .NotEmpty().WithMessage("El monto es requerido")
            .GreaterThan(0).WithMessage("El monto debe ser mayor que cero");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("La descripción es requerida")
            .MaximumLength(255).WithMessage("La descripción no puede exceder los 255 caracteres");

        RuleFor(x => x.PaymentMethod)
            .NotEmpty().WithMessage("El método de pago es requerido")
            .IsInEnum().WithMessage("El método de pago debe ser un valor válido");

        RuleFor(x => x.ReferenceNumber)
            .MaximumLength(50).WithMessage("El número de referencia no puede exceder los 50 caracteres");

        RuleFor(x => x.TransactionDate)
            .NotEmpty().WithMessage("La fecha de transacción es requerida")
            .LessThanOrEqualTo(DateTime.UtcNow).WithMessage("La fecha de transacción no puede ser futura");

        RuleFor(x => x.Category)
            .NotNull().WithMessage("La categoría es requerida");

        RuleFor(x => x.Car)
            .NotNull().WithMessage("El carro es requerido");

        RuleFor(x => x.Client)
            .NotNull().WithMessage("El cliente es requerido");

        RuleFor(x => x.Sale)
            .NotNull().WithMessage("La venta es requerida");
    }
}

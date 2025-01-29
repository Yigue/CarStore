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
            .NotEmpty().WithMessage("El método de pago es requerido");

        RuleFor(x => x.Category)
            .NotEmpty().WithMessage("La categoría es requerida");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("La descripción es requerida")
            .MaximumLength(255).WithMessage("La descripción no puede exceder los 255 caracteres");

        RuleFor(x => x.Car!)
            .SetValidator(new CarValidator())
            .When(x => x.Car != null);

        RuleFor(x => x.Client)
            .SetValidator(new ClientValidator())
            .When(x => x.Client != null);

        RuleFor(x => x.Sale!)
            .SetValidator(new SaleValidator())
            .When(x => x.Sale != null);
    }
}

public class CarValidator : AbstractValidator<Car>
{
    public CarValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("El ID del carro es requerido");
    }
}

public class ClientValidator : AbstractValidator<Client>
{
    public ClientValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("El ID del cliente es requerido");
    }
}

public class SaleValidator : AbstractValidator<Sale>
{
    public SaleValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("El ID de la venta es requerido");
    }
}

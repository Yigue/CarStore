using Domain.Sales.Attributes;
using FluentValidation;

namespace Application.Sales.Create;

internal sealed class CreateSaleCommandValidator : AbstractValidator<CreateSaleCommand>
{
    public CreateSaleCommandValidator()
    {
        RuleFor(x => x.CarId).NotEmpty();
        RuleFor(x => x.ClientId).NotEmpty();
        RuleFor(x => x.FinalPrice).NotEmpty().GreaterThan(0);
        RuleFor(x => x.PaymentMethod).IsInEnum();

        // A sale cannot be created as already Cancelled — Cancelled only makes
        // sense as a transition away from a Pending sale (see Sale.Cancel).
        RuleFor(x => x.Status)
            .NotEqual(SaleStatus.Cancelled)
            .When(x => x.Status.HasValue)
            .WithMessage("A sale cannot be created with Cancelled as its initial status.");

        RuleFor(x => x.SalespersonId)
            .NotEqual(Guid.Empty)
            .When(x => x.SalespersonId.HasValue)
            .WithMessage("SalespersonId cannot be an empty guid.");
    }
}

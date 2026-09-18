using Domain.Sales.Attributes;
using FluentValidation;

namespace Application.Sales.Create;

internal sealed class CreateSaleCommandValidator : AbstractValidator<CreateSaleCommand>
{
    public CreateSaleCommandValidator()
    {
        RuleFor(x => x.CarId).NotEmpty();
        RuleFor(x => x.ClientId).NotEmpty();
        // VEN-04: a sale converting an accepted quote may omit both and inherit them. Without a
        // quote to inherit from, they are still required — a sale with no price is not a sale.
        RuleFor(x => x.FinalPrice)
            .NotNull()
            .GreaterThan(0)
            .When(x => x.QuoteId is null)
            .WithMessage("FinalPrice is required unless the sale is created from an accepted quote.");

        RuleFor(x => x.FinalPrice)
            .GreaterThan(0)
            .When(x => x.FinalPrice.HasValue)
            .WithMessage("FinalPrice must be greater than 0.");

        RuleFor(x => x.PaymentMethod)
            .NotNull()
            .When(x => x.QuoteId is null)
            .WithMessage("PaymentMethod is required unless the sale is created from an accepted quote.");

        RuleFor(x => x.PaymentMethod)
            .IsInEnum()
            .When(x => x.PaymentMethod.HasValue);

        // VEN-03: the payment breakdown is optional, but the pieces that are sent have to make
        // sense on their own. Whether they add up to the price is the aggregate's business
        // (Sale.SetPaymentTerms) — it is the only place that knows the price.
        RuleFor(x => x.DownPayment).GreaterThanOrEqualTo(0).When(x => x.DownPayment.HasValue);
        RuleFor(x => x.TradeInValue).GreaterThanOrEqualTo(0).When(x => x.TradeInValue.HasValue);
        RuleFor(x => x.FinancedAmount).GreaterThanOrEqualTo(0).When(x => x.FinancedAmount.HasValue);
        RuleFor(x => x.InstallmentAmount).GreaterThanOrEqualTo(0).When(x => x.InstallmentAmount.HasValue);
        RuleFor(x => x.InstallmentCount).GreaterThan(0).When(x => x.InstallmentCount.HasValue);
        RuleFor(x => x.FinancingEntity).MaximumLength(120).When(x => x.FinancingEntity is not null);
        RuleFor(x => x.InvoiceNumber).MaximumLength(50).When(x => x.InvoiceNumber is not null);
        RuleFor(x => x.TransferFormNumber).MaximumLength(50).When(x => x.TransferFormNumber is not null);
        RuleFor(x => x.RegistrationNumber).MaximumLength(50).When(x => x.RegistrationNumber is not null);

        // ContractNumber is NOT NULL at the schema level (SaleConfiguration.cs) but had no rule
        // here — a missing value reached Postgres and came back as a raw 500 instead of a 400.
        RuleFor(x => x.ContractNumber).NotEmpty().MaximumLength(50);

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

using Application.Abstractions.Pricing;
using FluentValidation;

namespace Application.Quotes.Create;

internal sealed class CreateQuoteCommandValidator : AbstractValidator<CreateQuoteCommand>
{
    public CreateQuoteCommandValidator(IMoneyPolicy? moneyPolicy = null)
    {
        // sfc B0.2.3 / VB.2: with Money:RequireExplicitCurrency on, no implicit ARS default for new records.
        When(_ => moneyPolicy?.RequireExplicitCurrency == true, () =>
            RuleFor(x => x.Currency).NotEmpty().WithMessage("La moneda es obligatoria (ARS o USD)."));

        RuleFor(x => x.CarId).NotEmpty();
        RuleFor(x => x)
            .Must(x => x.ClientId.HasValue || x.LeadId.HasValue)
            .WithMessage("A quote must reference a ClientId, a LeadId, or both.");
        RuleFor(x => x.ProposedPrice).NotEmpty().GreaterThan(0);
        RuleFor(x => x.PaymentMethod).IsInEnum();
        RuleFor(x => x.Comments).MaximumLength(500);

        RuleFor(x => x.ValidUntil).NotEmpty().GreaterThan(DateTime.UtcNow);
    }
}

using FluentValidation;

namespace Application.Quotes.Create;

internal sealed class CreateQuoteCommandValidator : AbstractValidator<CreateQuoteCommand>
{
    public CreateQuoteCommandValidator()
    {
        RuleFor(x => x.CarId).NotEmpty();
        RuleFor(x => x)
            .Must(x => x.ClientId.HasValue ^ x.LeadId.HasValue)
            .WithMessage("A quote must reference exactly one of ClientId or LeadId.");
        RuleFor(x => x.ProposedPrice).NotEmpty().GreaterThan(0);
        RuleFor(x => x.PaymentMethod).IsInEnum();
        RuleFor(x => x.Comments).MaximumLength(500);

        RuleFor(x => x.ValidUntil).NotEmpty().GreaterThan(DateTime.UtcNow);
    }
}

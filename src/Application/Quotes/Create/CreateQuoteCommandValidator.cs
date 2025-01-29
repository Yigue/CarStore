using FluentValidation;

namespace Application.Quotes.Create;

internal sealed class CreateQuoteCommandValidator : AbstractValidator<CreateQuoteCommand>
{
    public CreateQuoteCommandValidator()
    {
        RuleFor(x => x.CarId).NotEmpty();
        RuleFor(x => x.ClientId).NotEmpty();
        RuleFor(x => x.ProposedPrice).NotEmpty().GreaterThan(0);
        RuleFor(x => x.Comments).NotEmpty().MaximumLength(500);

        RuleFor(x => x.ValidUntil).NotEmpty().GreaterThan(DateTime.UtcNow);
    }
}

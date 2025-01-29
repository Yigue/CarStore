using FluentValidation;

namespace Application.Quotes.Update;

internal sealed class UpdateQuoteCommandValidator : AbstractValidator<UpdateQuoteCommand>
{
    public UpdateQuoteCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.ProposedPrice).GreaterThan(0);
        RuleFor(x => x.ValidUntil).NotEmpty().GreaterThan(DateTime.UtcNow);
        RuleFor(x => x.Comments).NotEmpty().MaximumLength(500);
    }
}

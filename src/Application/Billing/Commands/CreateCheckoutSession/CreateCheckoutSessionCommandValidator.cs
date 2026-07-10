using FluentValidation;

namespace Application.Billing.Commands.CreateCheckoutSession;

public sealed class CreateCheckoutSessionCommandValidator : AbstractValidator<CreateCheckoutSessionCommand>
{
    public CreateCheckoutSessionCommandValidator()
    {
        RuleFor(x => x.DealerId)
            .NotEmpty()
            .WithMessage("DealerId is required.");

        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .WithMessage("A valid Email is required.");
    }
}

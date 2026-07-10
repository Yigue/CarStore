using FluentValidation;

namespace Application.Billing.Commands.HandleStripeWebhook;

public sealed class HandleStripeWebhookCommandValidator : AbstractValidator<HandleStripeWebhookCommand>
{
    public HandleStripeWebhookCommandValidator()
    {
        RuleFor(x => x.EventId)
            .NotEmpty()
            .WithMessage("EventId is required.");

        RuleFor(x => x.EventType)
            .NotEmpty()
            .WithMessage("EventType is required.");

        RuleFor(x => x.RawJson)
            .NotEmpty()
            .WithMessage("RawJson is required.");
    }
}

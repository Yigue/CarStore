using Domain.Webhooks;
using FluentValidation;

namespace Application.Webhooks.Update;

public sealed class UpdateWebhookSubscriptionCommandValidator : AbstractValidator<UpdateWebhookSubscriptionCommand>
{
    public UpdateWebhookSubscriptionCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();

        RuleFor(x => x.Url)
            .NotEmpty()
            .MaximumLength(2048)
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
                         (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            .WithMessage("Url must be an absolute http(s) URL.");

        RuleFor(x => x.EventTypes)
            .NotEmpty()
            .WithMessage("At least one event type must be subscribed.");

        RuleForEach(x => x.EventTypes)
            .Must(WebhookEventCatalog.IsValid)
            .WithMessage((_, eventType) => $"Unknown webhook event type: '{eventType}'.");
    }
}

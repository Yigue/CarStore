using FluentValidation;

namespace Application.Leads.RegisterWhatsAppInboundMessage;

internal sealed class RegisterWhatsAppInboundMessageCommandValidator : AbstractValidator<RegisterWhatsAppInboundMessageCommand>
{
    public RegisterWhatsAppInboundMessageCommandValidator()
    {
        RuleFor(x => x.DealerId).NotEmpty();
        RuleFor(x => x.FromPhoneE164).NotEmpty().MaximumLength(30);
        RuleFor(x => x.MessageText).NotEmpty().MaximumLength(4096);
    }
}

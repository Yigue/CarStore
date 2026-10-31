using Application.Abstractions.Messaging;

namespace Application.Leads.RegisterWhatsAppInboundMessage;

/// <summary>
/// sfc C0.4 / RN-046: a real inbound WhatsApp message, dispatched by the Cloud API webhook endpoint
/// after signature verification. Unlike <c>RegisterWhatsAppClickIntentCommand</c>, this has a real
/// contact identity (the sender's phone number), so it follows <c>CreateInquiryCommandHandler</c>'s
/// find-or-create shape, keyed by phone instead of email.
/// </summary>
public sealed record RegisterWhatsAppInboundMessageCommand(
    Guid DealerId,
    string FromPhoneE164,
    string MessageText,
    DateTime OccurredAtUtc) : ICommand<Guid>;

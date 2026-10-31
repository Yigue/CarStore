using Application.Abstractions.Messaging;

namespace Application.Leads.RegisterWhatsAppClickIntent;

/// <summary>
/// sfc C0.3 / B14: a visitor clicked "chat on WhatsApp" on a car's page. No contact information
/// exists yet — the visitor has not sent anything, just opened the composer — so this is
/// intentionally lighter than <c>CreateInquiryCommand</c>: it registers the intent itself as a
/// minimal Lead/LeadActivity so sales can see "someone clicked WhatsApp about car X", per design.md
/// ("por default ... se registra como intención").
/// </summary>
public sealed record RegisterWhatsAppClickIntentCommand(Guid? CarId, string Code) : ICommand<Guid>;

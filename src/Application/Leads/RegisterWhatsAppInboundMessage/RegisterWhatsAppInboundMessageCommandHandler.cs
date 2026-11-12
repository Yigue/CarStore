using Application.Abstractions;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Leads;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Leads.RegisterWhatsAppInboundMessage;

/// <summary>
/// sfc C0.4 / RN-046: a real WhatsApp message creates or appends a Lead, same shape as
/// <c>CreateInquiryCommandHandler.FindOrCreateLeadAsync</c> but keyed by phone number — the one
/// piece of identity an inbound WhatsApp message actually carries.
/// </summary>
internal sealed class RegisterWhatsAppInboundMessageCommandHandler(
    IApplicationDbContext context,
    IRoundRobinLeadAllocator allocator)
    : ICommandHandler<RegisterWhatsAppInboundMessageCommand, Guid>
{
    private static readonly LeadStatus[] ClosedStatuses =
        [LeadStatus.Ganado, LeadStatus.Perdido, LeadStatus.Archivado];

    public async Task<Result<Guid>> Handle(RegisterWhatsAppInboundMessageCommand command, CancellationToken cancellationToken)
    {
        // Runs from an AllowAnonymous webhook with no tenant context — scope by DealerId explicitly,
        // same reasoning as CreateInquiryCommandHandler.FindOrCreateLeadAsync.
        Lead? lead = await context.Leads
            .Where(l => l.DealerId == command.DealerId
                        && l.Phone == command.FromPhoneE164
                        && !ClosedStatuses.Contains(l.Status))
            .OrderByDescending(l => l.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (lead is null)
        {
            string placeholderEmail = $"whatsapp+{Sanitize(command.FromPhoneE164)}@leads.carstore.internal";
            lead = Lead.Create(
                command.DealerId,
                $"Contacto WhatsApp {command.FromPhoneE164}",
                placeholderEmail,
                command.FromPhoneE164,
                LeadSource.WhatsApp,
                command.OccurredAtUtc);

            Guid? agentId = await allocator.AllocateAsync(command.DealerId, cancellationToken);
            if (agentId.HasValue)
            {
                lead.AssignAgent(agentId.Value);
            }

            context.Leads.Add(lead);
        }

        string entry = $"[{command.OccurredAtUtc:yyyy-MM-dd HH:mm}] WhatsApp: {command.MessageText}";
        lead.UpdateNotes(string.IsNullOrWhiteSpace(lead.Notes) ? entry : $"{lead.Notes}\n{entry}");

        context.LeadActivities.Add(LeadActivity.Record(
            command.DealerId,
            lead.Id,
            LeadActivityType.NoteAdded,
            $"Mensaje de WhatsApp recibido: \"{Truncate(command.MessageText)}\"",
            command.OccurredAtUtc));

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(lead.Id);
    }

    private static string Sanitize(string phone) => phone.Trim().TrimStart('+');

    private static string Truncate(string text) => text.Length > 120 ? string.Concat(text.AsSpan(0, 117), "...") : text;
}

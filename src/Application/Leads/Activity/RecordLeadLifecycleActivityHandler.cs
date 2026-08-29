using Application.Abstractions.Data;
using Domain.Leads;
using Domain.Leads.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Leads.Activity;

/// <summary>Timeline entries for the lead's own events: created, reassigned, stage changed.</summary>
internal sealed class RecordLeadLifecycleActivityHandler(
    IApplicationDbContext context,
    LeadActivityRecorder recorder,
    IDateTimeProvider dateTimeProvider)
    : INotificationHandler<LeadCreatedDomainEvent>,
      INotificationHandler<LeadAssignedDomainEvent>,
      INotificationHandler<LeadStatusChangedDomainEvent>
{
    private static readonly Dictionary<LeadStatus, string> StatusLabels = new()
    {
        [LeadStatus.Nuevo] = "Nuevo",
        [LeadStatus.Contactado] = "Contactado",
        [LeadStatus.Demostracion] = "Demostración",
        [LeadStatus.Negociacion] = "Negociación",
        [LeadStatus.Ganado] = "Ganado",
        [LeadStatus.Perdido] = "Perdido",
        [LeadStatus.Archivado] = "Archivado",
    };

    private static readonly Dictionary<LeadLossReason, string> LossReasonLabels = new()
    {
        [LeadLossReason.Precio] = "precio",
        [LeadLossReason.Financiacion] = "financiación",
        [LeadLossReason.ComproEnOtra] = "compró en otra concesionaria",
        [LeadLossReason.Desistio] = "desistió",
        [LeadLossReason.Otro] = "otro motivo",
    };

    public Task Handle(LeadCreatedDomainEvent notification, CancellationToken cancellationToken) =>
        RecordAsync(
            notification.LeadId,
            LeadActivityType.Created,
            _ => "Lead creado",
            cancellationToken);

    public Task Handle(LeadAssignedDomainEvent notification, CancellationToken cancellationToken) =>
        RecordAsync(
            notification.LeadId,
            LeadActivityType.AgentAssigned,
            _ => "Lead asignado a un agente",
            cancellationToken,
            relatedEntityId: notification.AgentId,
            relatedEntityType: "User");

    public Task Handle(LeadStatusChangedDomainEvent notification, CancellationToken cancellationToken) =>
        RecordAsync(
            notification.LeadId,
            LeadActivityType.StatusChanged,
            lead => BuildStatusDescription(notification, lead),
            cancellationToken);

    /// <summary>
    /// The loss reason is captured on the lead and, until this timeline existed, was never shown
    /// anywhere. Folding it into the sentence is what finally makes "why did we lose this?"
    /// answerable from the history rather than only from the record's own field.
    /// </summary>
    private static string BuildStatusDescription(LeadStatusChangedDomainEvent notification, Lead lead)
    {
        string from = StatusLabels.GetValueOrDefault(notification.OldStatus, notification.OldStatus.ToString());
        string to = StatusLabels.GetValueOrDefault(notification.NewStatus, notification.NewStatus.ToString());

        string description = $"Estado: {from} → {to}";

        if (notification.NewStatus == LeadStatus.Perdido && lead.LossReason is { } reason)
        {
            description += $" (motivo: {LossReasonLabels.GetValueOrDefault(reason, reason.ToString())})";
        }

        return description;
    }

    private async Task RecordAsync(
        Guid leadId,
        LeadActivityType type,
        Func<Lead, string> describe,
        CancellationToken cancellationToken,
        Guid? relatedEntityId = null,
        string? relatedEntityType = null)
    {
        Lead? lead = await context.Leads.FirstOrDefaultAsync(l => l.Id == leadId, cancellationToken);

        if (lead is null)
        {
            return;
        }

        bool recorded = await recorder.RecordAsync(
            lead,
            type,
            describe(lead),
            dateTimeProvider.UtcNow,
            cancellationToken,
            relatedEntityId,
            relatedEntityType);

        if (recorded)
        {
            await context.SaveChangesAsync(cancellationToken);
        }
    }
}

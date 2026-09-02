using SharedKernel;
using Domain.Leads.Events;
using Domain.Shared.ValueObjects;

namespace Domain.Leads;

public sealed class Lead : Entity
{
    public string ClientName { get; private set; }
    public Email Email { get; private set; }
    public string Phone { get; private set; }
    public LeadStatus Status { get; private set; }
    public Guid? AssignedAgentId { get; private set; }
    public string? Notes { get; private set; }
    public LeadSource Source { get; private set; }
    public DateTime CreatedAt { get; private set; }

    // CRM Integration fields
    public Guid? InterestedVehicleId { get; private set; }
    public Guid? ConvertedClientId { get; private set; }
    public LeadLossReason? LossReason { get; private set; }

    /// <summary>
    /// UTC timestamp of the one-shot re-engagement email sent to Perdido leads
    /// (see <c>Infrastructure.BackgroundJobs.LeadReengagementJob</c>). Null until sent;
    /// once set, this lead is never re-engaged again (v1: one email ever per lead).
    /// </summary>
    public DateTime? ReengagementSentAtUtc { get; private set; }

    // Required by EF Core
    private Lead()
    {
    }

    public static Lead Create(
        Guid dealerId,
        string clientName,
        string email,
        string phone,
        LeadSource source,
        DateTime createdAt,
        Guid? interestedVehicleId = null)
    {
        if (string.IsNullOrWhiteSpace(clientName))
            throw new DomainException("ClientName cannot be empty");

        // Phone is deliberately optional. The domain's minimum is a way to reach the person, and
        // Email — a non-nullable value object that validates on construction — already guarantees
        // it. Public web enquiries are the main source of leads and all three forms treat the
        // phone as optional, so demanding it here would reject exactly the traffic this aggregate
        // exists to capture. Requiring a phone is a use-case policy, and it still lives in
        // CreateLeadCommandValidator for the dashboard's manual intake.

        var lead = new Lead();
        lead.SetDealer(dealerId);
        lead.Id = Guid.NewGuid();
        lead.ClientName = clientName;
        lead.Email = new Email(email);
        lead.Phone = phone;
        lead.Status = LeadStatus.Nuevo;
        lead.Source = source;
        lead.CreatedAt = createdAt;
        lead.AssignedAgentId = null;
        lead.InterestedVehicleId = interestedVehicleId;

        lead.Raise(new LeadCreatedDomainEvent(lead.Id, null));
        return lead;
    }

    public void AssignAgent(Guid agentId)
    {
        if (agentId == Guid.Empty)
            throw new DomainException("AgentId cannot be empty");

        AssignedAgentId = agentId;
        Raise(new LeadAssignedDomainEvent(Id, agentId));
    }

    /// <summary>
    /// Updates the lead status enforcing sequential progression rules.
    /// </summary>
    /// <param name="newStatus">Target status.</param>
    /// <param name="notes">Required when transitioning to <see cref="LeadStatus.Contactado"/>.</param>
    /// <param name="lossReason">Required when transitioning to <see cref="LeadStatus.Perdido"/>.</param>
    public void UpdateStatus(LeadStatus newStatus, string? notes, LeadLossReason? lossReason = null)
    {
        if (Status == LeadStatus.Ganado && newStatus < LeadStatus.Ganado)
            throw new DomainException("Un lead ganado no puede retroceder de etapa.");

        if (Status == LeadStatus.Archivado)
            throw new DomainException("A lead that has been archived cannot change status.");

        if (Status == newStatus)
            return;

        // Forward transitions only (excluding Perdido and Archivado as "end" states)
        var terminalOrBackward = newStatus != LeadStatus.Perdido
                                 && newStatus != LeadStatus.Archivado
                                 && (int)newStatus != (int)Status + 1;

        if (terminalOrBackward)
            throw new DomainException("Status transitions must be sequential — no stage skipping allowed.");

        // Transition-specific requirements
        // Contactado means a person owns this lead from here on. The gate used to be "notes were
        // typed", which recorded that someone wrote something without ever saying who is
        // responsible for the follow-up — leaving the lead ownerless in the one stage that
        // implies an owner. Notes are still persisted below when supplied.
        if (newStatus == LeadStatus.Contactado && AssignedAgentId is null)
            throw new DomainException("Moving to Contactado requires an assigned agent to own the follow-up.");

        if (newStatus == LeadStatus.Demostracion && InterestedVehicleId is null)
            throw new DomainException("Moving to Demostracion requires a linked interested vehicle.");

        if (newStatus == LeadStatus.Perdido && lossReason is null)
            throw new DomainException("Moving to Perdido requires a loss reason to be provided.");

        var oldStatus = Status;
        Status = newStatus;

        if (notes is not null)
            Notes = notes;

        if (newStatus == LeadStatus.Perdido && lossReason.HasValue)
            LossReason = lossReason;

        Raise(new LeadStatusChangedDomainEvent(Id, oldStatus, newStatus));
    }

    public void UpdateNotes(string? notes)
    {
        Notes = notes;
    }

    public void LinkVehicle(Guid vehicleId)
    {
        if (vehicleId == Guid.Empty)
            throw new DomainException("InterestedVehicleId vehicle cannot be empty.");

        InterestedVehicleId = vehicleId;
    }

    public void Archive()
    {
        if (Status == LeadStatus.Archivado)
            return;

        var oldStatus = Status;
        Status = LeadStatus.Archivado;
        Raise(new LeadStatusChangedDomainEvent(Id, oldStatus, LeadStatus.Archivado));
    }

    public void MarkConverted(Guid clientId)
    {
        if (clientId == Guid.Empty)
            throw new DomainException("ClientId cannot be empty when marking a lead as converted.");

        ConvertedClientId = clientId;
    }

    /// <summary>
    /// Stamps the one-shot lost-lead re-engagement email as sent. Called only from
    /// <c>Infrastructure.BackgroundJobs.LeadReengagementJob</c> after the email was
    /// accepted by the mail server — never before, so a transient SMTP failure leaves
    /// the lead eligible again on the next run instead of silently losing the outreach.
    /// </summary>
    public void MarkReengagementSent(DateTime sentAtUtc)
    {
        ReengagementSentAtUtc = sentAtUtc;
    }

    /// <summary>
    /// System-driven status change (e.g. quote accepted). Bypasses sequential UI enforcement
    /// but still prevents backward movement from Ganado.
    /// Only call this from application-layer event handlers, not from user-facing commands.
    /// </summary>
    public void ForceStatus(LeadStatus newStatus)
    {
        if (Status == LeadStatus.Ganado && newStatus < LeadStatus.Ganado)
            throw new DomainException("Un lead ganado no puede retroceder de etapa.");

        if (Status == newStatus)
            return;

        var oldStatus = Status;
        Status = newStatus;
        Raise(new LeadStatusChangedDomainEvent(Id, oldStatus, newStatus));
    }
}

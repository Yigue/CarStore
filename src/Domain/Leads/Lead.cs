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
        DateTime createdAt)
    {
        if (string.IsNullOrWhiteSpace(clientName))
            throw new DomainException("ClientName cannot be empty");
        if (string.IsNullOrWhiteSpace(phone))
            throw new DomainException("Phone cannot be empty");

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

    public void UpdateStatus(LeadStatus newStatus)
    {
        if (Status == LeadStatus.Ganado && newStatus < LeadStatus.Ganado)
            throw new DomainException("Un lead ganado no puede retroceder de etapa.");

        if (Status == newStatus)
            return;

        var oldStatus = Status;
        Status = newStatus;
        Raise(new LeadStatusChangedDomainEvent(Id, oldStatus, newStatus));
    }

    public void UpdateNotes(string? notes)
    {
        Notes = notes;
    }
}

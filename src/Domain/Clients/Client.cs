using SharedKernel;
using Domain.Sales;
using Domain.Clients.Attributes;
using Domain.Clients.Events;
using Domain.Shared.ValueObjects;

namespace Domain.Clients;

public sealed class Client : Entity, ISoftDeletable
{
    public string FirstName { get; private set; }
    public string LastName { get; private set; }
    public string DNI { get; private set; }
    public Email Email { get; private set; }
    public string Phone { get; private set; }
    public string Address { get; private set; }
    public string? City { get; private set; }
    public string? ZipCode { get; private set; }
    public string? Notes { get; private set; }
    public ClientStatus Status { get; private set; }
    public ClientType Type { get; private set; }
    public Guid? OriginLeadId { get; private set; }
    public List<Sale> Sales { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdateAt { get; private set; }

    // Soft-delete fields (mirrors Quote pattern — ADR-2)
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }
    public Guid? DeletedBy { get; private set; }

    // Enrichment fields (ADR-1 / REQ-001)
    public AcquisitionSource? AcquisitionSource { get; private set; }
    public Guid? AssignedAgentId { get; private set; }
    
    private Client() 
    {
        Sales = new List<Sale>();
    }
    
    public Client(
        Guid dealerId,
        string firstName,
        string lastName,
        string dni,
        string email,
        string phone,
        string address,
        DateTime date,
        ClientType type = ClientType.Individual,
        Guid? originLeadId = null,
        string? city = null,
        string? zipCode = null,
        string? notes = null)
    {
        SetDealer(dealerId);
        if (string.IsNullOrWhiteSpace(firstName))
            throw new DomainException("FirstName cannot be empty");
        if (string.IsNullOrWhiteSpace(lastName))
            throw new DomainException("LastName cannot be empty");

        FirstName = firstName;
        LastName = lastName;
        DNI = dni;
        Email = new Email(email);
        Phone = phone;
        Address = address;
        City = city;
        ZipCode = zipCode;
        Notes = notes;
        Status = ClientStatus.Active;
        Type = type;
        OriginLeadId = originLeadId;
        Sales = new List<Sale>();
        CreatedAt = date;
        UpdateAt = date;

        Raise(new ClientCreatedDomainEvent(Id, $"{FirstName} {LastName}"));
    }
    
    public void Update(
        string firstName,
        string lastName,
        string email,
        string phone,
        string address,
        DateTime updatedAt,
        string? city = null,
        string? zipCode = null,
        string? notes = null,
        ClientType? type = null)
    {
        if (string.IsNullOrWhiteSpace(firstName))
            throw new DomainException("FirstName cannot be empty");
        if (string.IsNullOrWhiteSpace(lastName))
            throw new DomainException("LastName cannot be empty");

        FirstName = firstName;
        LastName = lastName;
        Email = new Email(email);
        Phone = phone;
        Address = address;
        City = city;
        ZipCode = zipCode;
        Notes = notes;
        UpdateAt = updatedAt;

        if (type.HasValue && type.Value != Type)
        {
            ChangeType(type.Value, updatedAt);
        }
    }

    /// <summary>
    /// Replaces the <see cref="Type"/> value. Raises <see cref="ClientTypeChangedDomainEvent"/>
    /// when the value actually differs from the current one. No event is raised when the
    /// caller passes the same value (idempotent semantics).
    /// </summary>
    public void ChangeType(ClientType newType, DateTime occurredAtUtc)
    {
        if (newType == Type)
        {
            return;
        }

        ClientType previous = Type;
        Type = newType;
        UpdateAt = occurredAtUtc;

        Raise(new ClientTypeChangedDomainEvent(Id, previous, newType, occurredAtUtc));
    }
    
    public void Deactivate()
    {
        if (Status == ClientStatus.Inactive)
            return;
        
        Status = ClientStatus.Inactive;
        Raise(new ClientDeactivatedDomainEvent(Id));
    }
    
    public void Activate()
    {
        if (Status == ClientStatus.Active)
            return;
        
        Status = ClientStatus.Active;
    }

    public void SetProspect()
    {
        if (Status == ClientStatus.Prospect)
            return;

        Status = ClientStatus.Prospect;
    }

    public void SetVIP()
    {
        if (Status == ClientStatus.VIP)
            return;

        Status = ClientStatus.VIP;
    }

    /// <summary>
    /// Soft-deletes the client. Idempotent: raises no event if already deleted (ADR-2).
    /// </summary>
    public void Delete(Guid actorId, DateTime occurredAtUtc)
    {
        if (IsDeleted)
            return;

        IsDeleted = true;
        DeletedAtUtc = occurredAtUtc;
        DeletedBy = actorId;

        Raise(new ClientSoftDeletedDomainEvent(Id, occurredAtUtc, actorId));
    }

    /// <summary>
    /// Restores a soft-deleted client. Returns failure if the client is not deleted.
    /// </summary>
    public Result Restore(Guid actorId, DateTime occurredAtUtc)
    {
        if (!IsDeleted)
            return Result.Failure(ClientErrors.NotDeleted(Id));

        IsDeleted = false;
        DeletedAtUtc = null;
        DeletedBy = null;

        Raise(new ClientRestoredDomainEvent(Id, occurredAtUtc, actorId));

        return Result.Success();
    }

    /// <summary>
    /// Updates client notes. Max 2000 characters; null is allowed to clear notes.
    /// </summary>
    public Result UpdateNotes(string? notes, Guid? actorId, DateTime occurredAtUtc)
    {
        if (notes is not null && notes.Length > 2000)
            return Result.Failure(ClientErrors.NotesTooLong());

        Notes = notes;

        Raise(new ClientNotesUpdatedDomainEvent(Id, occurredAtUtc, actorId));

        return Result.Success();
    }
}

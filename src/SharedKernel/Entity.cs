namespace SharedKernel;

/// <summary>
/// Base class for all domain entities.
/// Contains DealerId for multi-tenancy support.
/// </summary>
public abstract class Entity
{
    public Guid Id { get; protected set; }
    
    /// <summary>
    /// Multi-tenancy: Every entity belongs to a Dealer (concesionaria).
    /// Required for all queries via EF Core Global Query Filters.
    /// </summary>
    public Guid DealerId { get; protected set; }
    
    private readonly List<IDomainEvent> _domainEvents = [];

    public List<IDomainEvent> DomainEvents => [.. _domainEvents];
    
    /// <summary>
    /// Sets the DealerId. Should be called from the constructor of derived entities.
    /// </summary>
    protected void SetDealer(Guid dealerId)
    {
        if (dealerId == Guid.Empty)
            throw new DomainException("DealerId cannot be empty");
        DealerId = dealerId;
    }

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }

    public void Raise(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }
}

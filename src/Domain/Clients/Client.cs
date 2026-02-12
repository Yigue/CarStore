using SharedKernel;
using Domain.Sales;
using Domain.Clients.Attributes;
using Domain.Clients.Events;
using Domain.Shared.ValueObjects;

namespace Domain.Clients;

public sealed class Client : Entity
{
    public string FirstName { get; private set; }
    public string LastName { get; private set; }
    public string DNI { get; private set; }
    public Email Email { get; private set; }
    public string Phone { get; private set; }
    public string Address { get; private set; }
    public ClientStatus Status { get; private set; }
    public List<Sale> Sales { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdateAt { get; private set; }
    
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
        DateTime date)
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
        Status = ClientStatus.Active;
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
        DateTime updatedAt)
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
        UpdateAt = updatedAt;
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
}

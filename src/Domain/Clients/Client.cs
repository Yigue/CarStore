using SharedKernel;
using Domain.Sales;
using Domain.Clients.Attributes;
using Domain.Clients.Events;

namespace Domain.Clients;

public sealed class Client : Entity
{
    public string FirstName { get;  set; }
    public string LastName { get;  set; }
    public string DNI { get;  set; }
    public string Email { get;  set; }
    public string Phone { get;  set; }
    public string Address { get;  set; }
    public ClientStatus Status { get;  set; }
    public List<Sale> Sales { get;  set; }
    public DateTime CreatedAt { get;  set; }
    public DateTime UpdateAt { get;  set; }
    
    private Client() { }
    
    public Client(
        string firstName,
        string lastName,
        string dni,
        string email,
        string phone,
        string address)
    {
        FirstName = firstName;
        LastName = lastName;
        DNI = dni;
        Email = email;
        Phone = phone;
        Address = address;
        Status = ClientStatus.Active;
        Sales = new List<Sale>();
        CreatedAt = DateTime.UtcNow;
        
        Raise(new ClientCreatedDomainEvent(Id, $"{FirstName} {LastName}"));
    }
    
}

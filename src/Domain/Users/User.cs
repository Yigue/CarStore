using Domain.Shared.ValueObjects;
using SharedKernel;

namespace Domain.Users;

public sealed class User : Entity
{
    // Private constructor for EF Core
    private User()
    {
    }

    public User(Guid dealerId, string email, string firstName, string lastName, string passwordHash)
    {
        SetDealer(dealerId);
        Id = Guid.NewGuid();
        Email = new Email(email);
        FirstName = firstName;
        LastName = lastName;
        PasswordHash = passwordHash;

        Raise(new UserRegisteredDomainEvent(Id));
    }

    public Email Email { get; private set; }
    public string FirstName { get; private set; }
    public string LastName { get; private set; }
    public string PasswordHash { get; private set; }
}

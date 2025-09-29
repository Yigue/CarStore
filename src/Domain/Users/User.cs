using SharedKernel;

namespace Domain.Users;

public sealed class User : Entity
{
    // Private constructor for EF Core
    private User()
    {
    }

    public User(string email, string firstName, string lastName, string passwordHash)
    {
        Id = Guid.NewGuid();
        Email = email;
        FirstName = firstName;
        LastName = lastName;
        PasswordHash = passwordHash;

        Raise(new UserRegisteredDomainEvent(Id));
    }

    public string Email { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string PasswordHash { get; set; }
}

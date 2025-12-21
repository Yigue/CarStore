using Domain.Clients;
using Domain.Clients.Attributes;
using Domain.Clients.Events;
using SharedKernel;

public class ClientTests
{
    private readonly Faker _faker = new();

    [Fact]
    public void Constructor_ShouldInitializePropertiesAndRaiseEvent()
    {
        var firstName = _faker.Name.FirstName();
        var lastName = _faker.Name.LastName();
        var dni = _faker.Random.ReplaceNumbers("########");
        var email = _faker.Internet.Email();
        var phone = _faker.Phone.PhoneNumber();
        var address = _faker.Address.FullAddress();

        var client = new Client(firstName, lastName, dni, email, phone, address);

        client.FirstName.Should().Be(firstName);
        client.LastName.Should().Be(lastName);
        client.DNI.Should().Be(dni);
        client.Email.Value.Should().Be(email);
        client.Phone.Should().Be(phone);
        client.Address.Should().Be(address);
        client.Status.Should().Be(ClientStatus.Active);
        client.Sales.Should().BeEmpty();
        client.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        client.DomainEvents.Should().ContainSingle();

        var domainEvent = client.DomainEvents.Single().Should().BeOfType<ClientCreatedDomainEvent>().Subject;
        domainEvent.ClientId.Should().Be(client.Id);
        domainEvent.FullName.Should().Be($"{firstName} {lastName}");
    }

    [Fact]
    public void ClientErrors_ShouldReturnExpectedValues()
    {
        var clientId = Guid.NewGuid();

        var alreadySold = ClientErrors.AlreadySold(clientId);
        alreadySold.Code.Should().Be("Clients.AlreadySold");
        alreadySold.Description.Should().Be($"The client with Id = '{clientId}' is already sold.");
        alreadySold.Type.Should().Be(ErrorType.Problem);

        var notFound = ClientErrors.NotFound(clientId);
        notFound.Code.Should().Be("Clients.NotFound");
        notFound.Description.Should().Be($"The client with the Id = '{clientId}' was not found");
        notFound.Type.Should().Be(ErrorType.NotFound);

        var notAll = ClientErrors.NotAllAtributes(clientId);
        notAll.Code.Should().Be("Clients.NotAllAttributes");
        notAll.Description.Should().Be($"The client with the Id = '{clientId}' was not found");
        notAll.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public void ClientEvents_ShouldContainClientId()
    {
        var clientId = Guid.NewGuid();
        var fullName = _faker.Name.FullName();

        var created = new ClientCreatedDomainEvent(clientId, fullName);
        created.ClientId.Should().Be(clientId);
        created.FullName.Should().Be(fullName);

        var deactivated = new ClientDeactivatedDomainEvent(clientId);
        deactivated.ClientId.Should().Be(clientId);

        var updated = new ClientUpdatedDomainEvent(clientId);
        updated.ClientId.Should().Be(clientId);
    }
}

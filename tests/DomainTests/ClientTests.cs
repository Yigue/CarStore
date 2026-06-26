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
        var dealerId = Guid.NewGuid();
        var date = DateTime.UtcNow;
        var client = new Client(dealerId, firstName, lastName, dni, email, phone, address, date);

        client.FirstName.Should().Be(firstName);
        client.LastName.Should().Be(lastName);
        client.DNI.Should().Be(dni);
        client.Email.Value.Should().Be(email.ToLowerInvariant());
        client.Phone.Should().Be(phone);
        client.Address.Should().Be(address);
        client.Status.Should().Be(ClientStatus.Active);
        client.Sales.Should().BeEmpty();
        client.CreatedAt.Should().Be(date);
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

    // PR1 — Client.ChangeType() + ClientTypeChangedDomainEvent (RED -> GREEN)

    [Fact]
    public void Constructor_Should_Default_Type_To_Individual()
    {
        var dealerId = Guid.NewGuid();
        var date = DateTime.UtcNow;
        var client = new Client(dealerId, "John", "Doe", "12345678", "john@test.com", "555", "Street 1", date);

        client.Type.Should().Be(ClientType.Individual);
    }

    [Fact]
    public void ChangeType_Should_Update_Type_And_Raise_ClientTypeChangedDomainEvent()
    {
        var dealerId = Guid.NewGuid();
        var occurredAt = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var client = new Client(dealerId, "John", "Doe", "12345678", "john@test.com", "555", "Street 1", occurredAt);
        // Clear the constructor event so we only assert the ChangeType event.
        client.ClearDomainEvents();

        client.ChangeType(ClientType.Corporate, occurredAt);

        client.Type.Should().Be(ClientType.Corporate);
        client.UpdateAt.Should().Be(occurredAt);

        client.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ClientTypeChangedDomainEvent>()
            .Subject.Should().Match<ClientTypeChangedDomainEvent>(e =>
                e.ClientId == client.Id
                && e.From == ClientType.Individual
                && e.To == ClientType.Corporate
                && e.OccurredAtUtc == occurredAt);
    }

    [Fact]
    public void ChangeType_Should_Not_Raise_Event_When_Value_Is_Same()
    {
        var dealerId = Guid.NewGuid();
        var occurredAt = DateTime.UtcNow;
        var client = new Client(dealerId, "John", "Doe", "12345678", "john@test.com", "555", "Street 1", occurredAt);
        client.ClearDomainEvents();

        // Already Individual by default -> no event should fire.
        client.ChangeType(ClientType.Individual, occurredAt);

        client.Type.Should().Be(ClientType.Individual);
        client.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Update_With_Type_Should_Change_Type_And_Raise_ClientTypeChangedDomainEvent()
    {
        var dealerId = Guid.NewGuid();
        var now = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var client = new Client(dealerId, "John", "Doe", "12345678", "john@test.com", "555", "Street 1", now);
        client.ClearDomainEvents();

        client.Update(
            "John", "Doe", "john@test.com", "555", "Street 1",
            now,
            city: null, zipCode: null, notes: null,
            type: ClientType.Corporate);

        client.Type.Should().Be(ClientType.Corporate);
        client.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ClientTypeChangedDomainEvent>();
    }
}

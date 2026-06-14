using Domain.Clients;

public class ClientExtensionTests
{
    [Fact]
    public void Constructor_ShouldPersist_CityZipCodeAndNotes()
    {
        var client = new Client(
            Guid.NewGuid(), "Juan", "Perez", "30111222", "juan@test.com", "111", "Av Corrientes 1234",
            DateTime.UtcNow, city: "Córdoba", zipCode: "5000", notes: "Cliente VIP");

        client.City.Should().Be("Córdoba");
        client.ZipCode.Should().Be("5000");
        client.Notes.Should().Be("Cliente VIP");
    }

    [Fact]
    public void Constructor_ShouldDefaultNull_WhenOptionalFieldsNotProvided()
    {
        var client = new Client(
            Guid.NewGuid(), "Juan", "Perez", "30111223", "juan2@test.com", "111", "Av Corrientes 1234",
            DateTime.UtcNow);

        client.City.Should().BeNull();
        client.ZipCode.Should().BeNull();
        client.Notes.Should().BeNull();
    }

    [Fact]
    public void Update_ShouldUpdate_CityZipCodeAndNotes()
    {
        var client = new Client(
            Guid.NewGuid(), "Juan", "Perez", "30111224", "juan3@test.com", "111", "Av Corrientes 1234",
            DateTime.UtcNow);

        client.Update(
            "Juan", "Perez", "juan3@test.com", "111", "Calle Nueva 123",
            DateTime.UtcNow, city: "Rosario", zipCode: "2000", notes: "Actualizado");

        client.City.Should().Be("Rosario");
        client.ZipCode.Should().Be("2000");
        client.Notes.Should().Be("Actualizado");
    }
}

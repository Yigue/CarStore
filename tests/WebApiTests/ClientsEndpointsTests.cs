using Application.Clients.GetAll;
using Domain.Clients;
using Infrastructure.Database;

namespace WebApiTests;

public class ClientsEndpointsTests
{
    [Fact]
    public async Task CreateClient_PersistsClient()
    {
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        var request = new
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "john@example.com",
            Phone = "123",
            Address = "Street 1",
            DNI = "111"
        };

        var response = await client.PostAsJsonAsync("/clients", request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var id = await response.Content.ReadFromJsonAsync<Guid>();

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.Clients.Should().ContainSingle(c => c.Id == id);
    }

    [Fact]
    public async Task CreateClient_ReturnsBadRequest_WhenInvalid()
    {
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        var request = new
        {
            FirstName = string.Empty,
            LastName = "Doe",
            Email = "john@example.com",
            Phone = "123",
            Address = "Street",
            DNI = "111"
        };

        var response = await client.PostAsJsonAsync("/clients", request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetClientById_ReturnsClient_WhenExists()
    {
        await using var factory = new CustomWebApplicationFactory();
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var entity = new Client("Jane", "Smith", "222", "jane@example.com", "456", "Road");
        context.Clients.Add(entity);
        await context.SaveChangesAsync();

        var httpClient = factory.CreateClient();
        var response = await httpClient.GetAsync($"/clients/{entity.Id}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ClientResponse>();
        result!.Id.Should().Be(entity.Id);
        result.FirstName.Should().Be("Jane");
    }

    [Fact]
    public async Task GetClientById_ReturnsNotFound_WhenMissing()
    {
        await using var factory = new CustomWebApplicationFactory();
        var httpClient = factory.CreateClient();
        var response = await httpClient.GetAsync($"/clients/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}

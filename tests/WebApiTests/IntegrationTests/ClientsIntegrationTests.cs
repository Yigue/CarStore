using Application.Clients.GetAll;
using Application.Clients.GetById;
using Domain.Clients;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;

namespace WebApiTests.IntegrationTests;

/// <summary>
/// Tests de integración para endpoints de Clients usando datos seedeados
/// </summary>
public class ClientsIntegrationTests
{
    [Fact]
    public async Task CreateClient_ShouldSucceed()
    {
        await using var factory = new CustomWebApplicationFactory();
        var token = await IntegrationTestHelpers.GetAdminTokenAsync(factory);
        var client = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(client, token);

        var request = new
        {
            FirstName = "Unique",
            LastName = "Client",
            DNI = "UNIQUE123",
            Email = "unique.client@example.com",
            Phone = "+54 11 1234-5678",
            Address = "Av. Corrientes 1234, Buenos Aires"
        };

        var response = await client.PostAsJsonAsync("/api/v1/clients", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var result = await response.Content.ReadFromJsonAsync<CreateResponse>(IntegrationTestHelpers.JsonOptions);
        var clientId = result!.id;
        clientId.Should().NotBe(Guid.Empty);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var createdClient = await context.Clients
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstAsync(c => c.Id == clientId);

        createdClient.FirstName.Should().Be("Unique");
        createdClient.LastName.Should().Be("Client");
        createdClient.Email.Value.Should().Be("unique.client@example.com");
        createdClient.DNI.Should().Be("UNIQUE123");
    }

    private sealed record CreateResponse(Guid id);

    [Fact]
    public async Task GetClients_ShouldReturnClients()
    {
        await using var factory = new CustomWebApplicationFactory();
        var token = await IntegrationTestHelpers.GetAdminTokenAsync(factory);
        var client = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(client, token);

        // Crear algunos clientes
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        var dealerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var client1 = new Domain.Clients.Client(
            dealerId,
            "María",
            "González",
            "87654321",
            "maria.gonzalez@example.com",
            "+54 11 9876-5432",
            "Av. Santa Fe 5678",
            DateTime.UtcNow);
        
        var client2 = new Domain.Clients.Client(
            dealerId,
            "Carlos",
            "Rodríguez",
            "11223344",
            "carlos.rodriguez@example.com",
            "+54 11 5555-1234",
            "Av. Libertador 9012",
            DateTime.UtcNow);
        
        context.Clients.AddRange(client1, client2);
        await context.SaveChangesAsync();

        var response = await client.GetAsync("/api/v1/clients");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var paginated = await response.Content.ReadFromJsonAsync<SharedKernel.PaginatedResult<Application.Clients.GetAll.ClientResponse>>(IntegrationTestHelpers.JsonOptions);
        paginated.Should().NotBeNull();
        paginated!.Items.Count.Should().BeGreaterThanOrEqualTo(2);
        paginated.Items.Should().Contain(c => c.Email == "maria.gonzalez@example.com");
        paginated.Items.Should().Contain(c => c.Email == "carlos.rodriguez@example.com");
    }

    [Fact]
    public async Task GetClientById_ShouldReturnClient()
    {
        await using var factory = new CustomWebApplicationFactory();
        var token = await IntegrationTestHelpers.GetAdminTokenAsync(factory);
        var client = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(client, token);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        var dealerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var testClient = new Domain.Clients.Client(
            dealerId,
            "Ana",
            "Martínez",
            "55667788",
            "ana.martinez@example.com",
            "+54 11 4444-5678",
            "Av. Córdoba 3456",
            DateTime.UtcNow);
        
        context.Clients.Add(testClient);
        await context.SaveChangesAsync();

        var response = await client.GetAsync($"/api/v1/clients/{testClient.Id}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<Application.Clients.GetAll.ClientResponse>(IntegrationTestHelpers.JsonOptions);
        result.Should().NotBeNull();
        result!.Id.Should().Be(testClient.Id);
        result.FirstName.Should().Be("Ana");
        result.LastName.Should().Be("Martínez");
        result.Email.Should().Be("ana.martinez@example.com");
    }

    [Fact]
    public async Task UpdateClient_ShouldSucceed()
    {
        await using var factory = new CustomWebApplicationFactory();
        var token = await IntegrationTestHelpers.GetAdminTokenAsync(factory);
        var client = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(client, token);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        var dealerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var testClient = new Domain.Clients.Client(
            dealerId,
            "Luis",
            "Fernández",
            "99887766",
            "luis.fernandez@example.com",
            "+54 11 3333-2222",
            "Av. Cabildo 7890",
            DateTime.UtcNow);
        
        context.Clients.Add(testClient);
        await context.SaveChangesAsync();

        var updateRequest = new
        {
            FirstName = "Luis",
            LastName = "Fernández",
            DNI = "99887766",
            Email = "luis.fernandez.updated@example.com",
            Phone = "+54 11 3333-9999",
            Address = "Av. Cabildo 7890, Piso 5",
            Status = 0 // Active
        };

        var response = await client.PutAsJsonAsync($"/api/v1/clients/{testClient.Id}", updateRequest);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var updatedClient = await context.Clients.IgnoreQueryFilters().AsNoTracking().FirstAsync(c => c.Id == testClient.Id);
        updatedClient.Email.Value.Should().Be("luis.fernandez.updated@example.com");
        updatedClient.Phone.Should().Be("+54 11 3333-9999");
    }

    [Fact]
    public async Task DeleteRestoreAndGetDeleted_ShouldBehaveCorrectly()
    {
        await using var factory = new CustomWebApplicationFactory();
        var token = await IntegrationTestHelpers.GetAdminTokenAsync(factory);
        var client = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(client, token);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        var dealerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var testClient = new Domain.Clients.Client(
            dealerId,
            "SoftDelete",
            "IntegrationTest",
            "77889900",
            "softdelete.test@example.com",
            "+54 11 9999-8888",
            "Test Street 123",
            DateTime.UtcNow);
        
        context.Clients.Add(testClient);
        await context.SaveChangesAsync();

        // 1. Soft Delete
        var deleteResponse = await client.DeleteAsync($"/api/v1/clients/{testClient.Id}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Client should not be returned in regular list
        var getListResponse = await client.GetAsync("/api/v1/clients");
        getListResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var activeList = await getListResponse.Content.ReadFromJsonAsync<SharedKernel.PaginatedResult<Application.Clients.GetAll.ClientResponse>>(IntegrationTestHelpers.JsonOptions);
        activeList!.Items.Should().NotContain(c => c.Id == testClient.Id);

        // 2. Get Deleted Clients
        var getDeletedResponse = await client.GetAsync("/api/v1/clients/deleted");
        getDeletedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var deletedList = await getDeletedResponse.Content.ReadFromJsonAsync<SharedKernel.PaginatedResult<Application.Clients.GetAll.ClientResponse>>(IntegrationTestHelpers.JsonOptions);
        deletedList!.Items.Should().Contain(c => c.Id == testClient.Id);

        // 3. Restore Client
        var restoreResponse = await client.PostAsync($"/api/v1/clients/{testClient.Id}/restore", new StringContent(string.Empty));
        restoreResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify client is restored (visible in active list again)
        var getListResponse2 = await client.GetAsync("/api/v1/clients");
        getListResponse2.StatusCode.Should().Be(HttpStatusCode.OK);
        var activeList2 = await getListResponse2.Content.ReadFromJsonAsync<SharedKernel.PaginatedResult<Application.Clients.GetAll.ClientResponse>>(IntegrationTestHelpers.JsonOptions);
        activeList2!.Items.Should().Contain(c => c.Id == testClient.Id);
    }
}


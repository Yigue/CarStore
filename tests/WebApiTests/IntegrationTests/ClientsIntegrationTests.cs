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
            FirstName = "Juan",
            LastName = "Pérez",
            DNI = "12345678",
            Email = "juan.perez@example.com",
            Phone = "+54 11 1234-5678",
            Address = "Av. Corrientes 1234, Buenos Aires"
        };

        var response = await client.PostAsJsonAsync("/clients", request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var clientId = await response.Content.ReadFromJsonAsync<Guid>();
        clientId.Should().NotBe(Guid.Empty);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        var createdClient = await context.Clients.FirstAsync(c => c.Id == clientId);
        createdClient.FirstName.Should().Be("Juan");
        createdClient.LastName.Should().Be("Pérez");
        createdClient.Email.Value.Should().Be("juan.perez@example.com");
        createdClient.DNI.Should().Be("12345678");
    }

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
        
        var client1 = new Domain.Clients.Client(
            "María",
            "González",
            "87654321",
            "maria.gonzalez@example.com",
            "+54 11 9876-5432",
            "Av. Santa Fe 5678");
        
        var client2 = new Domain.Clients.Client(
            "Carlos",
            "Rodríguez",
            "11223344",
            "carlos.rodriguez@example.com",
            "+54 11 5555-1234",
            "Av. Libertador 9012");
        
        context.Clients.AddRange(client1, client2);
        await context.SaveChangesAsync();

        var response = await client.GetAsync("/clients");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var clients = await response.Content.ReadFromJsonAsync<List<Application.Clients.GetAll.ClientResponse>>();
        clients.Should().NotBeNull();
        clients!.Count.Should().BeGreaterThanOrEqualTo(2);
        clients.Should().Contain(c => c.Email == "maria.gonzalez@example.com");
        clients.Should().Contain(c => c.Email == "carlos.rodriguez@example.com");
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
        
        var testClient = new Domain.Clients.Client(
            "Ana",
            "Martínez",
            "55667788",
            "ana.martinez@example.com",
            "+54 11 4444-5678",
            "Av. Córdoba 3456");
        
        context.Clients.Add(testClient);
        await context.SaveChangesAsync();

        var response = await client.GetAsync($"/clients/{testClient.Id}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<Application.Clients.GetById.ClientResponse>();
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
        
        var testClient = new Domain.Clients.Client(
            "Luis",
            "Fernández",
            "99887766",
            "luis.fernandez@example.com",
            "+54 11 3333-2222",
            "Av. Cabildo 7890");
        
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

        var response = await client.PutAsJsonAsync($"/clients/{testClient.Id}", updateRequest);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var updatedClient = await context.Clients.FirstAsync(c => c.Id == testClient.Id);
        updatedClient.Email.Value.Should().Be("luis.fernandez.updated@example.com");
        updatedClient.Phone.Should().Be("+54 11 3333-9999");
    }
}


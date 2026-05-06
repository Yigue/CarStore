using Application.Clients.GetAll;
using Domain.Clients;
using Infrastructure.Database;
using System.Net.Http.Json;
using System.Net;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;

namespace WebApiTests;

public class ClientsEndpointsTests
{
    private sealed record CreateResponse(Guid id);

    [Fact]
    public async Task CreateClient_PersistsClient()
    {
        await using var factory = new CustomWebApplicationFactory();
        var token = await IntegrationTestHelpers.GetAdminTokenAsync(factory);
        
        var client = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(client, token);

        var request = new
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "john@example.com",
            Phone = "123",
            Address = "Street 1",
            DNI = "111"
        };

        var response = await client.PostAsJsonAsync("/api/v1/clients", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<CreateResponse>();
        var id = result!.id;

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.Clients.IgnoreQueryFilters().Should().ContainSingle(c => c.Id == id);
    }

    [Fact]
    public async Task CreateClient_ReturnsBadRequest_WhenInvalid()
    {
        await using var factory = new CustomWebApplicationFactory();
        var token = await IntegrationTestHelpers.GetAdminTokenAsync(factory);
        
        var client = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(client, token);

        var request = new
        {
            FirstName = string.Empty,
            LastName = "Doe",
            Email = "john@example.com",
            Phone = "123",
            Address = "Street",
            DNI = "111"
        };

        var response = await client.PostAsJsonAsync("/api/v1/clients", request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetClientById_ReturnsClient_WhenExists()
    {
        await using var factory = new CustomWebApplicationFactory();
        var token = await IntegrationTestHelpers.GetAdminTokenAsync(factory);
        
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        var dealerId = Guid.Parse(CustomWebApplicationFactory.AdminDealerId);
        var entity = new Client(dealerId, "Jane", "Smith", "222", "jane@example.com", "456", "Road", DateTime.UtcNow);
        context.Clients.Add(entity);
        await context.SaveChangesAsync();

        var httpClient = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(httpClient, token);
        
        var response = await httpClient.GetAsync($"/api/v1/clients/{entity.Id}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ClientResponse>();
        result!.Id.Should().Be(entity.Id);
        result.FirstName.Should().Be("Jane");
    }

    [Fact]
    public async Task GetClientById_ReturnsNotFound_WhenMissing()
    {
        await using var factory = new CustomWebApplicationFactory();
        var token = await IntegrationTestHelpers.GetAdminTokenAsync(factory);
        
        var httpClient = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(httpClient, token);
        
        var response = await httpClient.GetAsync($"/api/v1/clients/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}

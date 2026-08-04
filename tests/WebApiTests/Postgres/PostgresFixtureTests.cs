using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace WebApiTests.Postgres;

[Collection("PostgresCollection")]
[Trait("Category", "Postgres")]
public class PostgresFixtureTests : IAsyncLifetime
{
    private readonly PostgresWebApplicationFactory _factory;

    public PostgresFixtureTests(PostgresFixture fixture)
    {
        _factory = new PostgresWebApplicationFactory(fixture.GetConnectionString());
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task PostgresFixture_ShouldBootAndMigrateSuccessfully()
    {
        var act = async () => await _factory.InitializeDatabaseAsync();
        await act.Should().NotThrowAsync();
    }
}

using System.Threading.Tasks;
using Testcontainers.PostgreSql;
using Xunit;

namespace WebApiTests.Postgres;

public class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .WithDatabase("carstore_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public string GetConnectionString() => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}

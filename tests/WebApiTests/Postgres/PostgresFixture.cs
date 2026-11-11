using System;
using System.Threading.Tasks;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace WebApiTests.Postgres;

/// <summary>
/// A real Postgres for the <c>Category=Postgres</c> tests.
/// <para>
/// Default: a throwaway Testcontainers instance. Where Docker is not available (some CI runners and
/// sandboxes), set <c>POSTGRES_TEST_ADMIN_CONNECTION</c> to a superuser connection string of an
/// existing server: the fixture then creates a fresh, uniquely named database on it for this run and
/// drops it afterwards, so runs never see each other's data.
/// </para>
/// </summary>
public class PostgresFixture : IAsyncLifetime
{
    private const string AdminConnectionVariable = "POSTGRES_TEST_ADMIN_CONNECTION";

    private readonly string? _adminConnection = Environment.GetEnvironmentVariable(AdminConnectionVariable);
    private readonly string _externalDatabase = $"carstore_test_{Guid.NewGuid():N}";

    private readonly PostgreSqlContainer? _container;

    public PostgresFixture()
    {
        if (string.IsNullOrWhiteSpace(_adminConnection))
        {
            _container = new PostgreSqlBuilder()
                .WithImage("postgres:17-alpine")
                .WithDatabase("carstore_test")
                .WithUsername("postgres")
                .WithPassword("postgres")
                .Build();
        }
    }

    public string GetConnectionString() => _container is not null
        ? _container.GetConnectionString()
        : new NpgsqlConnectionStringBuilder(_adminConnection!) { Database = _externalDatabase }.ConnectionString;

    public async Task InitializeAsync()
    {
        if (_container is not null)
        {
            await _container.StartAsync();
            return;
        }

        await using var admin = new NpgsqlConnection(_adminConnection);
        await admin.OpenAsync();
        await using var create = admin.CreateCommand();
        create.CommandText = $"CREATE DATABASE \"{_externalDatabase}\"";
        await create.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
            return;
        }

        NpgsqlConnection.ClearAllPools();
        await using var admin = new NpgsqlConnection(_adminConnection);
        await admin.OpenAsync();
        await using var drop = admin.CreateCommand();
        drop.CommandText = $"DROP DATABASE IF EXISTS \"{_externalDatabase}\" WITH (FORCE)";
        await drop.ExecuteNonQueryAsync();
    }
}

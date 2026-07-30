using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Infrastructure.Database;
using MediatR;
using Application.Abstractions.Tenancy;

public class Program
{
    private sealed class NoOpPublisher : IPublisher
    {
        public Task Publish(object notification, System.Threading.CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, System.Threading.CancellationToken cancellationToken = default)
            where TNotification : INotification => Task.CompletedTask;
    }
    private sealed class NoOpTenantService : ICurrentTenantService
    {
        public Guid DealerId => Guid.Empty;
        public bool HasTenant => false;
    }

    public static async Task Main()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .UseSnakeCaseNamingConvention()
            .Options;
        
        var context = new ApplicationDbContext(options, new NoOpPublisher(), new NoOpTenantService());
        try {
            await context.Database.EnsureCreatedAsync();
        } catch (Exception ex) {
            Console.WriteLine("Error EnsureCreated: " + ex.Message);
        }

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table';";
        using var reader = await cmd.ExecuteReaderAsync();
        Console.WriteLine("Tables:");
        while (await reader.ReadAsync())
        {
            Console.WriteLine("- " + reader.GetString(0));
        }
    }
}

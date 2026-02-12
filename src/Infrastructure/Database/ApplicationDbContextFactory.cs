using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using MediatR;
using Moq; // Using Moq or a dummy implementation for IPublisher
using System.IO;
using Application.Abstractions.Tenancy;

namespace Infrastructure.Database;

public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.Development.json", optional: true)
            .Build();

        var builder = new DbContextOptionsBuilder<ApplicationDbContext>();
        var connectionString = configuration.GetConnectionString("Database") 
                               ?? "Host=localhost;Port=5432;Database=carstore;Username=postgres;Password=postgres";

        builder.UseNpgsql(connectionString, b => 
        {
            b.MigrationsAssembly("Infrastructure");
            b.MigrationsHistoryTable("__EFMigrationsHistory", "public");
        });
        builder.UseSnakeCaseNamingConvention();

        // Create a dummy IPublisher since we don't need real events during migrations
        var dummyPublisher = new DummyPublisher();
        
        // Create a no-tenant service for migrations (bypasses query filters)
        var noTenantService = new NoTenantForMigrations();

        return new ApplicationDbContext(builder.Options, dummyPublisher, noTenantService);
    }

    private class DummyPublisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification => Task.CompletedTask;
    }
    
    private class NoTenantForMigrations : ICurrentTenantService
    {
        public Guid DealerId => Guid.Empty;
        public bool HasTenant => false;
    }
}


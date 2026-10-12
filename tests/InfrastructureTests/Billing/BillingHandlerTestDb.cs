using Application.Abstractions.Messaging;
using Application.Abstractions.Tenancy;
using Domain.Billing;
using Domain.DealerSettings;
using Domain.Users;
using Infrastructure.Database;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfrastructureTests.Billing;

/// <summary>Sqlite-backed real model plus a seeded dealer, used by the A2.2 billing notification handler tests.</summary>
internal sealed class BillingHandlerTestDb : IDisposable
{
    public const string CustomerId = "cus_a22";

    private readonly SqliteConnection _connection;

    public ApplicationDbContext Db { get; }
    public FakeEmailService Email { get; } = new();
    public Guid DealerId { get; } = Guid.NewGuid();
    public DealerSettings Settings { get; private set; } = null!;
    public DealerSubscription Subscription { get; private set; } = null!;
    public User Owner { get; private set; } = null!;

    public BillingHandlerTestDb()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .UseSnakeCaseNamingConvention()
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
            .Options;
        Db = new ApplicationDbContext(options, new NoOpPublisher(), new NoTenant());
        Db.Database.EnsureCreated();
    }

    /// <summary>Dealer with settings, an Admin role, an owner (oldest Admin), a newer Admin and a Seller; active subscription.</summary>
    public async Task SeedAsync(string? billingEmail = null, string status = "active")
    {
        Settings = new DealerSettings(DealerId, "Autos del Sur", "contacto@autosdelsur.test");
        Settings.SetBillingEmail(billingEmail);
        Db.DealerSettings.Add(Settings);

        var adminRole = new Role(DealerId, "Admin", "Administrator");
        var sellerRole = new Role(DealerId, "Seller", "Seller");
        Db.Roles.AddRange(adminRole, sellerRole);
        await Db.SaveChangesAsync();

        Owner = new User(DealerId, "owner@autosdelsur.test", "Marta", "Gomez", "hash", adminRole.Id);
        Db.Users.Add(Owner);
        await Db.SaveChangesAsync();
        await Task.Delay(30);

        Db.Users.Add(new User(DealerId, "newer-admin@autosdelsur.test", "Pablo", "Ruiz", "hash", adminRole.Id));
        Db.Users.Add(new User(DealerId, "seller@autosdelsur.test", "Sofia", "Paz", "hash", sellerRole.Id));

        Subscription = DealerSubscription.Create(DealerId, CustomerId, "sub_a22", "price_1");
        if (status == "active")
        {
            Subscription.Activate("sub_a22", DateTime.UtcNow, DateTime.UtcNow.AddMonths(1));
        }

        Subscription.ClearDomainEvents();
        Db.DealerSubscriptions.Add(Subscription);
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();
    }

    public void Dispose()
    {
        Db.Dispose();
        _connection.Dispose();
    }

    private sealed class NoTenant : ICurrentTenantService
    {
        public Guid DealerId => Guid.Empty;
        public bool HasTenant => false;
    }

    private sealed class NoOpPublisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification => Task.CompletedTask;
    }
}

internal sealed class FakeEmailService : IEmailService
{
    public List<SentEmail> Sent { get; } = [];
    public bool ThrowOnSend { get; set; }

    public Task SendEmailAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
    {
        if (ThrowOnSend)
        {
            throw new InvalidOperationException("smtp down");
        }

        Sent.Add(new SentEmail(to, subject, body));
        return Task.CompletedTask;
    }
}

internal sealed record SentEmail(string To, string Subject, string Body);

internal static class BillingHandlerTestLogger
{
    public static ILogger<T> For<T>() => NullLogger<T>.Instance;
}

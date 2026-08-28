using System.Linq;
using Application.Abstractions.Tenancy;
using FluentAssertions;
using Infrastructure.Database;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

/// <summary>
/// Eight foreign keys point at <c>Car</c>, and which of them cascade is a commercial decision,
/// not a mapping detail. Three of the dependents — Quote, Sale, FinancialTransaction — are the
/// dealership's commercial record. If any of those ever flipped to <see cref="DeleteBehavior.Cascade"/>,
/// deleting one vehicle would silently destroy the sales and transactions attached to it, and
/// nothing in the test suite would notice until an auditor did.
///
/// The three that DO cascade are parts of the vehicle itself: its images, its reconditioning
/// tasks, and the owned Money value object. Those have no meaning once the car is gone.
///
/// This test pins both sets. It is deliberately exhaustive: an unlisted new dependent fails the
/// count assertion, forcing whoever adds it to make the cascade decision consciously.
/// </summary>
public class CarReferenceDeleteBehaviorTests
{
    private sealed class NoOpPublisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification => Task.CompletedTask;
    }

    private sealed class NoOpTenantService : ICurrentTenantService
    {
        public Guid DealerId => Guid.Empty;
        public bool HasTenant => false;
    }

    /// <summary>Dependents whose rows must survive — and therefore block — a vehicle delete.</summary>
    private static readonly string[] MustBlockDelete =
        ["Appointment", "FinancialTransaction", "Lead", "Quote", "Sale"];

    /// <summary>Dependents that are part of the vehicle and are meaningless without it.</summary>
    private static readonly string[] MustCascade =
        ["CarImage", "Money", "ReconditioningTask"];

    private static Dictionary<string, IForeignKey> CarForeignKeys()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite("Filename=:memory:")
            .Options;

        using var context = new ApplicationDbContext(options, new NoOpPublisher(), new NoOpTenantService());

        return context.Model.GetEntityTypes()
            .SelectMany(e => e.GetForeignKeys())
            .Where(fk => fk.PrincipalEntityType.ClrType == typeof(Domain.Cars.Car))
            .ToDictionary(fk => fk.DeclaringEntityType.ClrType.Name, StringComparer.Ordinal);
    }

    [Fact]
    public void CommercialRecords_Should_RestrictVehicleDeletion()
    {
        Dictionary<string, IForeignKey> foreignKeys = CarForeignKeys();

        foreach (string dependent in MustBlockDelete)
        {
            foreignKeys.Should().ContainKey(dependent);
            foreignKeys[dependent].DeleteBehavior.Should().Be(
                DeleteBehavior.Restrict,
                "{0} rows outlive the vehicle they reference; cascading would destroy commercial history",
                dependent);
        }
    }

    [Fact]
    public void VehicleOwnedRecords_Should_CascadeWithTheVehicle()
    {
        Dictionary<string, IForeignKey> foreignKeys = CarForeignKeys();

        foreach (string dependent in MustCascade)
        {
            foreignKeys.Should().ContainKey(dependent);
            foreignKeys[dependent].DeleteBehavior.Should().Be(
                DeleteBehavior.Cascade,
                "{0} is part of the vehicle and has no meaning once it is gone",
                dependent);
        }
    }

    [Fact]
    public void EveryDependentOfCar_Should_BeClassified()
    {
        Dictionary<string, IForeignKey> foreignKeys = CarForeignKeys();

        foreignKeys.Keys.Should().BeEquivalentTo(
            MustBlockDelete.Concat(MustCascade),
            "a new foreign key to Car must be classified as blocking or cascading here, so the " +
            "decision is made deliberately rather than inherited from an EF Core convention");
    }
}

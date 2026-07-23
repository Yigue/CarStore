using System.Linq;
using Application.Abstractions.Tenancy;
using FluentAssertions;
using Infrastructure.Database;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using SharedKernel;
using Xunit;

namespace ArchitectureTests.Layers;

/// <summary>
/// RED→GREEN (PR1 saas-custom-domains task 1.6.5):
/// EF model snapshot MUST declare a UNIQUE index on DealerSettings.HostName
/// (per spec: wildcard-subdomain-routing §DealerSettings.HostName Is Uniquely Indexed)
/// and on DealerSettings.Slug (per spec dealer-slug MUST be NOT NULL + UNIQUE).
/// </summary>
public class TenantIndexesTests : BaseTest
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

    private static IModel BuildSnapshotModel()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        using var ctx = new ApplicationDbContext(options, new NoOpPublisher(), new NoOpTenantService());
        return ctx.Model;
    }

    [Fact]
    public void DealerSettings_Should_Have_UniqueIndex_On_HostName()
    {
        // Arrange
        var model = BuildSnapshotModel();
        var dealerEntity = model.GetEntityTypes()
            .Single(t => t.ClrType == typeof(Domain.DealerSettings.DealerSettings));

        // Act
        var uniqueHostIndex = dealerEntity.GetIndexes()
            .FirstOrDefault(i => i.IsUnique
                && i.Properties.Any(p => p.Name == nameof(Domain.DealerSettings.DealerSettings.HostName)));

        // Assert
        uniqueHostIndex.Should().NotBeNull(
            "spec wildcard-subdomain-routing §DealerSettings.HostName Is Uniquely Indexed requires a UNIQUE index on HostName.");
    }

    [Fact]
    public void DealerSettings_Should_Have_UniqueIndex_On_Slug()
    {
        // Arrange
        var model = BuildSnapshotModel();
        var dealerEntity = model.GetEntityTypes()
            .Single(t => t.ClrType == typeof(Domain.DealerSettings.DealerSettings));

        // Act
        var uniqueSlugIndex = dealerEntity.GetIndexes()
            .FirstOrDefault(i => i.IsUnique
                && i.Properties.Any(p => p.Name == nameof(Domain.DealerSettings.DealerSettings.Slug)));

        // Assert
        uniqueSlugIndex.Should().NotBeNull(
            "spec wildcard-subdomain-routing §DealerSettings.HostName Is Uniquely Indexed also requires the dealer Slug to be NOT NULL + UNIQUE.");
    }

    [Fact]
    public void DealerSettings_Should_Have_Exactly_One_UniqueIndex_On_HostName()
    {
        // Arrange — saas-custom-domains-followups item 2: two separately-authored
        // migrations used to each add a UNIQUE index on HostName under different
        // names (IX_DealerSettings_HostName_Unique and ux_dealer_settings_host_name).
        // The redundant one was dropped; only ux_dealer_settings_host_name remains.
        var model = BuildSnapshotModel();
        var dealerEntity = model.GetEntityTypes()
            .Single(t => t.ClrType == typeof(Domain.DealerSettings.DealerSettings));

        // Act
        var uniqueHostIndexes = dealerEntity.GetIndexes()
            .Where(i => i.IsUnique
                && i.Properties.Count == 1
                && i.Properties[0].Name == nameof(Domain.DealerSettings.DealerSettings.HostName))
            .ToList();

        // Assert
        uniqueHostIndexes.Should().ContainSingle(
            "the redundant IX_DealerSettings_HostName_Unique index was dropped in favor of " +
            "the single ux_dealer_settings_host_name index — only one UNIQUE index on HostName " +
            "should remain in the model.");
        uniqueHostIndexes.Single().GetDatabaseName().Should().Be("ux_dealer_settings_host_name");
    }

    [Fact]
    public void DealerSettings_Should_Have_Partial_LookupIndex_On_HostName_Where_Active()
    {
        // Arrange
        var model = BuildSnapshotModel();
        var dealerEntity = model.GetEntityTypes()
            .Single(t => t.ClrType == typeof(Domain.DealerSettings.DealerSettings));

        // Act — partial lookup index backing anonymous Host lookups
        var lookupIndex = dealerEntity.GetIndexes()
            .FirstOrDefault(i => i.Properties.Any(p => p.Name == nameof(Domain.DealerSettings.DealerSettings.IsActive)));

        // Assert
        lookupIndex.Should().NotBeNull(
            "spec wildcard-subdomain-routing §Anonymous Host Lookup Uses a Dedicated Index requires a filtered index " +
            "on host_name with predicate IsActive = true to keep the hot path small and exclude soft-deleted dealers.");
    }
}

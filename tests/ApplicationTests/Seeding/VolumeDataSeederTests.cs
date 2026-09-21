using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Infrastructure.Database.SeedData;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Application.UnitTests.Seeding;

public class VolumeDataSeederTests
{
    private static readonly Guid DefaultDealerId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestApplicationDbContext(options);
    }

    private static IConfiguration CreateConfiguration(bool enabled, int? carCount = null, int? leadCount = null)
    {
        var values = new Dictionary<string, string?>
        {
            ["Seeding:VolumeData:Enabled"] = enabled.ToString(),
        };
        if (carCount is not null) values["Seeding:VolumeData:CarCount"] = carCount.Value.ToString();
        if (leadCount is not null) values["Seeding:VolumeData:LeadCount"] = leadCount.Value.ToString();

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    private static async Task SeedBrandsAsync(TestApplicationDbContext context)
    {
        var toyota = new Domain.Cars.Attributes.Marca("Toyota");
        var ford = new Domain.Cars.Attributes.Marca("Ford");
        context.Marca.AddRange(toyota, ford);
        await context.SaveChangesAsync();

        context.Modelo.AddRange(
            new Domain.Cars.Attributes.Modelo("Corolla", toyota.Id),
            new Domain.Cars.Attributes.Modelo("Hilux", toyota.Id),
            new Domain.Cars.Attributes.Modelo("Ranger", ford.Id));
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task SeedAsync_DoesNothing_WhenNotEnabled()
    {
        using TestApplicationDbContext context = CreateContext();
        await SeedBrandsAsync(context);
        IConfiguration configuration = CreateConfiguration(enabled: false);

        await VolumeDataSeeder.SeedAsync(context, configuration, NullLogger.Instance, CancellationToken.None);

        (await context.Cars.IgnoreQueryFilters().CountAsync()).Should().Be(0);
        (await context.Leads.IgnoreQueryFilters().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task SeedAsync_GeneratesConfiguredCounts_WhenEnabled()
    {
        using TestApplicationDbContext context = CreateContext();
        await SeedBrandsAsync(context);
        IConfiguration configuration = CreateConfiguration(enabled: true, carCount: 50, leadCount: 80);

        await VolumeDataSeeder.SeedAsync(context, configuration, NullLogger.Instance, CancellationToken.None);

        var cars = await context.Cars.IgnoreQueryFilters().Where(c => c.DealerId == DefaultDealerId).ToListAsync();
        var leads = await context.Leads.IgnoreQueryFilters().Where(l => l.DealerId == DefaultDealerId).ToListAsync();
        cars.Should().HaveCount(50);
        leads.Should().HaveCount(80);
        cars.Select(c => c.Patente.Value).Distinct().Should().HaveCount(50, "every generated plate must be unique");
    }

    [Fact]
    public async Task SeedAsync_IsIdempotent_WhenRunTwiceWithTheSameTarget()
    {
        using TestApplicationDbContext context = CreateContext();
        await SeedBrandsAsync(context);
        IConfiguration configuration = CreateConfiguration(enabled: true, carCount: 30, leadCount: 40);

        await VolumeDataSeeder.SeedAsync(context, configuration, NullLogger.Instance, CancellationToken.None);
        await VolumeDataSeeder.SeedAsync(context, configuration, NullLogger.Instance, CancellationToken.None);

        (await context.Cars.IgnoreQueryFilters().CountAsync(c => c.DealerId == DefaultDealerId)).Should().Be(30);
        (await context.Leads.IgnoreQueryFilters().CountAsync(l => l.DealerId == DefaultDealerId)).Should().Be(40);
    }

    [Fact]
    public async Task SeedAsync_TopsUp_WhenTargetIsRaisedOnASecondRun()
    {
        using TestApplicationDbContext context = CreateContext();
        await SeedBrandsAsync(context);

        await VolumeDataSeeder.SeedAsync(context, CreateConfiguration(true, 20, 20), NullLogger.Instance, CancellationToken.None);
        await VolumeDataSeeder.SeedAsync(context, CreateConfiguration(true, 45, 45), NullLogger.Instance, CancellationToken.None);

        (await context.Cars.IgnoreQueryFilters().CountAsync(c => c.DealerId == DefaultDealerId)).Should().Be(45);
        (await context.Leads.IgnoreQueryFilters().CountAsync(l => l.DealerId == DefaultDealerId)).Should().Be(45);
    }

    [Fact]
    public async Task SeedAsync_DoesNotGenerateCars_WhenNoBrandsExistYet()
    {
        using TestApplicationDbContext context = CreateContext();
        IConfiguration configuration = CreateConfiguration(enabled: true, carCount: 10, leadCount: 10);

        await VolumeDataSeeder.SeedAsync(context, configuration, NullLogger.Instance, CancellationToken.None);

        (await context.Cars.IgnoreQueryFilters().CountAsync()).Should().Be(0);
    }
}

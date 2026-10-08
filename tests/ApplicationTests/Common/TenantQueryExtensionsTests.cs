using System;
using System.Linq;
using System.Threading.Tasks;
using Application.Abstractions.Tenancy;
using Application.Common;
using Domain.Leads;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using SharedKernel;

namespace Application.UnitTests.Common;

public class TenantQueryExtensionsTests
{
    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly Guid TenantB = Guid.NewGuid();

    private static TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestApplicationDbContext(options);
    }

    [Fact]
    public async Task IgnoreQueryFiltersForTenant_WhenTenantActive_ReturnsOnlyMatchingTenantRows()
    {
        using var context = CreateContext();
        var leadA = Lead.Create(TenantA, "Lead A", "a@test.com", "111", LeadSource.Web, DateTime.UtcNow);
        var leadB = Lead.Create(TenantB, "Lead B", "b@test.com", "222", LeadSource.Web, DateTime.UtcNow);
        context.Leads.AddRange(leadA, leadB);
        await context.SaveChangesAsync();

        var tenantMock = new Mock<ICurrentTenantService>();
        tenantMock.SetupGet(t => t.HasTenant).Returns(true);
        tenantMock.SetupGet(t => t.DealerId).Returns(TenantA);

        var results = await context.Leads
            .IgnoreQueryFiltersForTenant(tenantMock.Object)
            .ToListAsync();

        results.Should().ContainSingle();
        results[0].Id.Should().Be(leadA.Id);
    }

    [Fact]
    public async Task IgnoreQueryFiltersForTenant_WhenNoTenant_ReturnsEmptyFailClosed()
    {
        using var context = CreateContext();
        var leadA = Lead.Create(TenantA, "Lead A", "a@test.com", "111", LeadSource.Web, DateTime.UtcNow);
        context.Leads.Add(leadA);
        await context.SaveChangesAsync();

        var tenantMock = new Mock<ICurrentTenantService>();
        tenantMock.SetupGet(t => t.HasTenant).Returns(false);

        var results = await context.Leads
            .IgnoreQueryFiltersForTenant(tenantMock.Object)
            .ToListAsync();

        results.Should().BeEmpty("fail-closed must return zero rows when HasTenant is false");
    }

    [Fact]
    public async Task IgnoreQueryFiltersForTenant_IncludesArchivedForSameTenant()
    {
        using var context = CreateContext();
        var archivedLead = Lead.Create(TenantA, "Archived A", "arch@test.com", "333", LeadSource.Web, DateTime.UtcNow);
        archivedLead.Archive();
        context.Leads.Add(archivedLead);
        await context.SaveChangesAsync();

        var tenantMock = new Mock<ICurrentTenantService>();
        tenantMock.SetupGet(t => t.HasTenant).Returns(true);
        tenantMock.SetupGet(t => t.DealerId).Returns(TenantA);

        var results = await context.Leads
            .IgnoreQueryFiltersForTenant(tenantMock.Object)
            .ToListAsync();

        results.Should().ContainSingle();
        results[0].Id.Should().Be(archivedLead.Id);
    }
}

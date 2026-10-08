using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions.Tenancy;
using Application.Leads.GetAll;
using Domain.Leads;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Application.UnitTests.Leads;

public class GetLeadsQueryHandlerTests
{
    private static readonly Guid DealerId = Guid.NewGuid();

    private static TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestApplicationDbContext(options);
    }

    [Fact]
    public async Task Handle_Should_ExcludeArchivedLeadsByDefault_WhenStatusNotSpecifiedAndIncludeArchivedIsFalse()
    {
        using var context = CreateContext();

        var activeLead = Lead.Create(DealerId, "Lead Activo", "activo@test.com", "123", LeadSource.Web, DateTime.UtcNow);
        var archivedLead = Lead.Create(DealerId, "Lead Archivado", "archivado@test.com", "456", LeadSource.Web, DateTime.UtcNow);
        archivedLead.Archive();

        context.Leads.AddRange(activeLead, archivedLead);
        await context.SaveChangesAsync();

        var query = new GetLeadsQuery(Status: null, IncludeArchived: false);
        var handler = new GetLeadsQueryHandler(context);

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].Id.Should().Be(activeLead.Id);
    }

    [Fact]
    public async Task Handle_Should_IncludeArchivedLeads_WhenIncludeArchivedIsTrue()
    {
        using var context = CreateContext();

        var activeLead = Lead.Create(DealerId, "Lead Activo", "activo@test.com", "123", LeadSource.Web, DateTime.UtcNow);
        var archivedLead = Lead.Create(DealerId, "Lead Archivado", "archivado@test.com", "456", LeadSource.Web, DateTime.UtcNow);
        archivedLead.Archive();

        context.Leads.AddRange(activeLead, archivedLead);
        await context.SaveChangesAsync();

        var query = new GetLeadsQuery(Status: null, IncludeArchived: true);
        var handler = new GetLeadsQueryHandler(context);

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_Should_ReturnArchivedLead_WhenStatusIsExplicitlyArchivado()
    {
        using var context = CreateContext();

        var activeLead = Lead.Create(DealerId, "Lead Activo", "activo@test.com", "123", LeadSource.Web, DateTime.UtcNow);
        var archivedLead = Lead.Create(DealerId, "Lead Archivado", "archivado@test.com", "456", LeadSource.Web, DateTime.UtcNow);
        archivedLead.Archive();

        context.Leads.AddRange(activeLead, archivedLead);
        await context.SaveChangesAsync();

        var query = new GetLeadsQuery(Status: LeadStatus.Archivado);
        var handler = new GetLeadsQueryHandler(context);

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].Id.Should().Be(archivedLead.Id);
    }

    [Fact]
    public async Task Handle_Should_NotReturnLeadsFromOtherDealer_WhenIncludeArchivedIsTrue()
    {
        using var context = CreateContext();
        var otherDealerId = Guid.NewGuid();

        var activeLeadA = Lead.Create(DealerId, "Lead Activo A", "activoA@test.com", "123", LeadSource.Web, DateTime.UtcNow);
        var archivedLeadA = Lead.Create(DealerId, "Lead Archivado A", "archivadoA@test.com", "456", LeadSource.Web, DateTime.UtcNow);
        archivedLeadA.Archive();

        var activeLeadB = Lead.Create(otherDealerId, "Lead Activo B", "activoB@test.com", "789", LeadSource.Web, DateTime.UtcNow);
        var archivedLeadB = Lead.Create(otherDealerId, "Lead Archivado B", "archivadoB@test.com", "012", LeadSource.Web, DateTime.UtcNow);
        archivedLeadB.Archive();

        context.Leads.AddRange(activeLeadA, archivedLeadA, activeLeadB, archivedLeadB);
        await context.SaveChangesAsync();

        var tenantService = new Mock<ICurrentTenantService>();
        tenantService.SetupGet(t => t.DealerId).Returns(DealerId);
        tenantService.SetupGet(t => t.HasTenant).Returns(true);

        var query = new GetLeadsQuery(Status: null, IncludeArchived: true);
        var handler = new GetLeadsQueryHandler(context, tenantService.Object);

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value.Select(l => l.Id).Should().Contain(new[] { activeLeadA.Id, archivedLeadA.Id });
        result.Value.Select(l => l.Id).Should().NotContain(new[] { activeLeadB.Id, archivedLeadB.Id });
    }

    [Fact]
    public async Task Handle_Should_NotReturnArchivedLeadsFromOtherDealer_WhenStatusIsArchivado()
    {
        using var context = CreateContext();
        var otherDealerId = Guid.NewGuid();

        var archivedLeadA = Lead.Create(DealerId, "Lead Archivado A", "archivadoA@test.com", "456", LeadSource.Web, DateTime.UtcNow);
        archivedLeadA.Archive();

        var archivedLeadB = Lead.Create(otherDealerId, "Lead Archivado B", "archivadoB@test.com", "012", LeadSource.Web, DateTime.UtcNow);
        archivedLeadB.Archive();

        context.Leads.AddRange(archivedLeadA, archivedLeadB);
        await context.SaveChangesAsync();

        var tenantService = new Mock<ICurrentTenantService>();
        tenantService.SetupGet(t => t.DealerId).Returns(DealerId);
        tenantService.SetupGet(t => t.HasTenant).Returns(true);

        var query = new GetLeadsQuery(Status: LeadStatus.Archivado);
        var handler = new GetLeadsQueryHandler(context, tenantService.Object);

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].Id.Should().Be(archivedLeadA.Id);
    }
}

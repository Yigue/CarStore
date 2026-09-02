using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Leads.GetAll;
using Domain.Leads;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

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
}

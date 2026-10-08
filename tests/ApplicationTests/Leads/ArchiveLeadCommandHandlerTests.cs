using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions.Tenancy;
using Application.Leads.Archive;
using Domain.Leads;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using SharedKernel;
using Xunit;

namespace Application.UnitTests.Leads;

public class ArchiveLeadCommandHandlerTests
{
    private static readonly Guid CurrentDealerId = Guid.NewGuid();
    private static readonly Guid OtherDealerId = Guid.NewGuid();

    private static TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestApplicationDbContext(options);
    }

    private static Mock<ICurrentTenantService> CreateTenantMock(Guid dealerId, bool hasTenant = true)
    {
        var mock = new Mock<ICurrentTenantService>();
        mock.SetupGet(t => t.DealerId).Returns(dealerId);
        mock.SetupGet(t => t.HasTenant).Returns(hasTenant);
        return mock;
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenLeadBelongsToAnotherTenant()
    {
        using var context = CreateContext();
        var foreignLead = Lead.Create(
            OtherDealerId,
            "Carlos Perez",
            "carlos@other.com",
            "12345678",
            LeadSource.Web,
            DateTime.UtcNow);

        context.Leads.Add(foreignLead);
        await context.SaveChangesAsync();

        var tenantService = CreateTenantMock(CurrentDealerId);
        var handler = new ArchiveLeadCommandHandler(context, tenantService.Object);

        var result = await handler.Handle(new ArchiveLeadCommand(foreignLead.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(LeadErrors.NotFound(foreignLead.Id).Code);

        var untouched = await context.Leads.FirstAsync(l => l.Id == foreignLead.Id);
        untouched.Status.Should().NotBe(LeadStatus.Archivado);
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenNoTenantContext()
    {
        using var context = CreateContext();
        var lead = Lead.Create(
            CurrentDealerId,
            "Maria Gomez",
            "maria@current.com",
            "87654321",
            LeadSource.Web,
            DateTime.UtcNow);

        context.Leads.Add(lead);
        await context.SaveChangesAsync();

        var tenantService = CreateTenantMock(Guid.Empty, hasTenant: false);
        var handler = new ArchiveLeadCommandHandler(context, tenantService.Object);

        var result = await handler.Handle(new ArchiveLeadCommand(lead.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(LeadErrors.NotFound(lead.Id).Code);
    }

    [Fact]
    public async Task Handle_Should_ArchiveLead_WhenLeadBelongsToCurrentTenant()
    {
        using var context = CreateContext();
        var ownLead = Lead.Create(
            CurrentDealerId,
            "Juan Perez",
            "juan@current.com",
            "11223344",
            LeadSource.Portal,
            DateTime.UtcNow);

        context.Leads.Add(ownLead);
        await context.SaveChangesAsync();

        var tenantService = CreateTenantMock(CurrentDealerId);
        var handler = new ArchiveLeadCommandHandler(context, tenantService.Object);

        var result = await handler.Handle(new ArchiveLeadCommand(ownLead.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var archived = await context.Leads.IgnoreQueryFilters().FirstAsync(l => l.Id == ownLead.Id);
        archived.Status.Should().Be(LeadStatus.Archivado);
    }

    [Fact]
    public async Task Handle_Should_BeIdempotent_WhenLeadAlreadyArchivedInCurrentTenant()
    {
        using var context = CreateContext();
        var ownLead = Lead.Create(
            CurrentDealerId,
            "Pedro Lopez",
            "pedro@current.com",
            "99887766",
            LeadSource.Referral,
            DateTime.UtcNow);
        ownLead.Archive();

        context.Leads.Add(ownLead);
        await context.SaveChangesAsync();

        var tenantService = CreateTenantMock(CurrentDealerId);
        var handler = new ArchiveLeadCommandHandler(context, tenantService.Object);

        var result = await handler.Handle(new ArchiveLeadCommand(ownLead.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var archived = await context.Leads.IgnoreQueryFilters().FirstAsync(l => l.Id == ownLead.Id);
        archived.Status.Should().Be(LeadStatus.Archivado);
    }
}

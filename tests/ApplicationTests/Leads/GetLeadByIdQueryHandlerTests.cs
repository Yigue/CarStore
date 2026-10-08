using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions.Tenancy;
using Application.Leads.GetById;
using Domain.Leads;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using SharedKernel;

namespace Application.UnitTests.Leads;

public class GetLeadByIdQueryHandlerTests
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
        mock.Setup(t => t.DealerId).Returns(dealerId);
        mock.Setup(t => t.HasTenant).Returns(hasTenant);
        return mock;
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenLeadBelongsToAnotherTenant()
    {
        // Arrange
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
        var handler = new GetLeadByIdQueryHandler(context, tenantService.Object);

        // Act
        var result = await handler.Handle(new GetLeadByIdQuery(foreignLead.Id), CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(LeadErrors.NotFound(foreignLead.Id).Code);
    }

    [Fact]
    public async Task Handle_Should_ReturnSuccess_WhenLeadBelongsToCurrentTenant()
    {
        // Arrange
        using var context = CreateContext();
        var ownLead = Lead.Create(
            CurrentDealerId,
            "Juan Gomez",
            "juan@current.com",
            "87654321",
            LeadSource.Portal,
            DateTime.UtcNow);

        context.Leads.Add(ownLead);
        await context.SaveChangesAsync();

        var tenantService = CreateTenantMock(CurrentDealerId);
        var handler = new GetLeadByIdQueryHandler(context, tenantService.Object);

        // Act
        var result = await handler.Handle(new GetLeadByIdQuery(ownLead.Id), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(ownLead.Id);
        result.Value.ClientName.Should().Be("Juan Gomez");
    }

    [Fact]
    public async Task Handle_Should_ReturnSuccess_WhenLeadBelongsToCurrentTenant_AndIsArchived()
    {
        // Arrange: Archived lead for CurrentDealerId should still be visible by its own dealer
        using var context = CreateContext();
        var archivedLead = Lead.Create(
            CurrentDealerId,
            "Maria Lopez",
            "maria@current.com",
            "11223344",
            LeadSource.Web,
            DateTime.UtcNow);
        archivedLead.Archive();

        context.Leads.Add(archivedLead);
        await context.SaveChangesAsync();

        var tenantService = CreateTenantMock(CurrentDealerId);
        var handler = new GetLeadByIdQueryHandler(context, tenantService.Object);

        // Act
        var result = await handler.Handle(new GetLeadByIdQuery(archivedLead.Id), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(archivedLead.Id);
    }
}

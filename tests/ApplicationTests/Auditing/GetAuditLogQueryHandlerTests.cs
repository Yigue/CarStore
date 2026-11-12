using Application.Abstractions.Tenancy;
using Application.Auditing.GetAuditLog;
using Domain.Auditing;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Application.UnitTests.Auditing;

/// <summary>sfc B2.2.4 / A20: GET audit-log reads only the caller's dealer, filtered by entity and date range.</summary>
public class GetAuditLogQueryHandlerTests
{
    private static readonly Guid DealerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherDealerId = Guid.Parse("99999999-9999-9999-9999-999999999999");
    private static readonly DateTime T0 = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    private static TestApplicationDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<TestApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static GetAuditLogQueryHandler CreateHandler(TestApplicationDbContext context, bool hasTenant = true)
    {
        var tenant = new Mock<ICurrentTenantService>();
        tenant.SetupGet(t => t.HasTenant).Returns(hasTenant);
        tenant.SetupGet(t => t.DealerId).Returns(DealerId);
        return new GetAuditLogQueryHandler(context, tenant.Object);
    }

    private static DealerAuditLogEntry Entry(Guid dealerId, string entity, DateTime at, string property = "Price") =>
        DealerAuditLogEntry.Record(dealerId, entity, Guid.NewGuid(), property, "1 ARS", "2 ARS", Guid.NewGuid(), "r", "c", at);

    [Fact]
    public async Task Handle_Should_ReturnOnlyTheCallersDealer_NewestFirst()
    {
        using var context = CreateContext();
        context.DealerAuditLogs.AddRange(
            Entry(DealerId, "Car", T0), Entry(DealerId, "Sale", T0.AddHours(2), "Status"), Entry(OtherDealerId, "Car", T0.AddHours(1)));
        await context.SaveChangesAsync();

        var result = await CreateHandler(context).Handle(new GetAuditLogQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Select(i => i.EntityType).Should().Equal("Sale", "Car");
        result.Value.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_Should_FilterByEntityAndDateRange()
    {
        using var context = CreateContext();
        context.DealerAuditLogs.AddRange(
            Entry(DealerId, "Car", T0), Entry(DealerId, "Car", T0.AddDays(5)), Entry(DealerId, "Sale", T0.AddDays(1), "Status"));
        await context.SaveChangesAsync();

        var byEntity = await CreateHandler(context).Handle(new GetAuditLogQuery(Entity: "car"), CancellationToken.None);
        byEntity.Value.Items.Should().HaveCount(2).And.OnlyContain(i => i.EntityType == "Car");

        var byRange = await CreateHandler(context).Handle(
            new GetAuditLogQuery(Entity: "Car", From: T0.AddDays(1), To: T0.AddDays(6)), CancellationToken.None);
        byRange.Value.Items.Should().ContainSingle().Which.OccurredAtUtc.Should().Be(T0.AddDays(5));
    }

    [Fact]
    public async Task Handle_Should_Paginate()
    {
        using var context = CreateContext();
        for (int i = 0; i < 5; i++) context.DealerAuditLogs.Add(Entry(DealerId, "Car", T0.AddMinutes(i)));
        await context.SaveChangesAsync();

        var page2 = await CreateHandler(context).Handle(new GetAuditLogQuery(Page: 2, PageSize: 2), CancellationToken.None);

        page2.Value.Items.Should().HaveCount(2);
        page2.Value.TotalCount.Should().Be(5);
    }

    [Fact]
    public async Task Handle_Should_Fail_WhenThereIsNoTenant()
    {
        using var context = CreateContext();

        var result = await CreateHandler(context, hasTenant: false).Handle(new GetAuditLogQuery(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Validator_Should_RejectPageSizeAbove100_AndInvertedRange()
    {
        var validator = new GetAuditLogQueryValidator();

        validator.Validate(new GetAuditLogQuery(PageSize: 101)).IsValid.Should().BeFalse();
        validator.Validate(new GetAuditLogQuery(From: T0.AddDays(1), To: T0)).IsValid.Should().BeFalse();
        validator.Validate(new GetAuditLogQuery(From: T0, To: T0.AddDays(1))).IsValid.Should().BeTrue();
    }
}

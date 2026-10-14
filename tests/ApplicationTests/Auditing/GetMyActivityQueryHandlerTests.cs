using Application.Abstractions.Authentication;
using Application.Abstractions.Tenancy;
using Application.Auditing.GetMyActivity;
using Domain.Auditing;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Application.UnitTests.Auditing;

/// <summary>
/// sfc B5.3 / design C21: "Historial de actividad" reads the dealer audit trail filtered by actor. A user sees only their
/// own changes in their own dealer, newest first and capped, without needing audit:read.
/// </summary>
public class GetMyActivityQueryHandlerTests
{
    private static readonly Guid DealerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherDealerId = Guid.Parse("99999999-9999-9999-9999-999999999999");
    private static readonly Guid Me = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid Colleague = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly DateTime T0 = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    private static DealerAuditLogEntry Entry(Guid dealerId, Guid actor, DateTime at) =>
        DealerAuditLogEntry.Record(dealerId, "Car", Guid.NewGuid(), "Price", "1 ARS", "2 ARS", actor, "r", "c", at);

    private static GetMyActivityQueryHandler Handler(TestApplicationDbContext context)
    {
        var tenant = new Mock<ICurrentTenantService>();
        tenant.SetupGet(t => t.HasTenant).Returns(true);
        tenant.SetupGet(t => t.DealerId).Returns(DealerId);
        var user = new Mock<IUserContext>();
        user.SetupGet(u => u.UserId).Returns(Me);
        return new GetMyActivityQueryHandler(context, tenant.Object, user.Object);
    }

    [Fact]
    public async Task Handle_Should_ReturnOnlyMyEntries_InMyDealer_NewestFirst_Capped()
    {
        using var context = new TestApplicationDbContext(
            new DbContextOptionsBuilder<TestApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        for (int i = 0; i < GetMyActivityQuery.MaxEntries + 5; i++)
            context.DealerAuditLogs.Add(Entry(DealerId, Me, T0.AddMinutes(i)));
        context.DealerAuditLogs.AddRange(
            Entry(DealerId, Colleague, T0.AddDays(1)),
            Entry(OtherDealerId, Me, T0.AddDays(2)));
        await context.SaveChangesAsync();

        var result = await Handler(context).Handle(new GetMyActivityQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(GetMyActivityQuery.MaxEntries);
        result.Value.Should().OnlyContain(e => e.ActorUserId == Me);
        result.Value[0].OccurredAtUtc.Should().Be(T0.AddMinutes(GetMyActivityQuery.MaxEntries + 4));
        result.Value.Select(e => e.OccurredAtUtc).Should().BeInDescendingOrder();
    }
}

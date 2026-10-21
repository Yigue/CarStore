using Application.Abstractions.Authentication;
using Application.Abstractions.Tenancy;
using Application.Reports.GetCommissionsReport;
using Application.UnitTests;
using Domain.Commissions;
using Domain.Shared.ValueObjects;
using Domain.Users;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using SharedKernel;

namespace ApplicationTests.Commissions;

/// <summary>
/// sfc B4.2 / RN-010: without <c>commissions:read-all</c>, a salesperson cannot see another
/// salesperson's commission. <c>GetCommissionsReportQueryHandler</c> does the real narrowing — the
/// endpoint only gates on the narrower <c>commissions:read-own</c> (RequireAuthorization supports a
/// single policy), so a role with just <c>read-own</c> must still be rejected in-handler for a
/// mismatched <c>SalespersonId</c>.
/// </summary>
public class CommissionPermissionsTests
{
    private static readonly Guid DealerId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private static TestApplicationDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<TestApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, DealerId);

    private static ICurrentTenantService TenantService()
    {
        var mock = new Mock<ICurrentTenantService>();
        mock.SetupGet(t => t.HasTenant).Returns(true);
        mock.SetupGet(t => t.DealerId).Returns(DealerId);
        return mock.Object;
    }

    private static IUserContext CallerContext(Guid userId)
    {
        var mock = new Mock<IUserContext>();
        mock.SetupGet(u => u.UserId).Returns(userId);
        return mock.Object;
    }

    /// <summary>Seeds a Role with the given permissions and a User pointing at it; returns the User.Id.</summary>
    private static Guid SeedUser(TestApplicationDbContext context, string roleName, params string[] permissions)
    {
        var role = new Role(DealerId, roleName, roleName);
        foreach (string permission in permissions)
            role.AddPermission(permission);
        context.Roles.Add(role);

        var user = new User(DealerId, $"{Guid.NewGuid():N}@test.com", "Nom", "Bre", "hash", role.Id);
        context.Users.Add(user);
        return user.Id;
    }

    private static SaleCommission SeedCommission(TestApplicationDbContext context, Guid salespersonId, decimal amount, DateTime calculatedAtUtc)
    {
        var result = new CommissionResult(new Money(amount * 20, "ARS"), 5m, new Money(amount, "ARS"), IsConfigured: true);
        SaleCommission commission = SaleCommission.Create(DealerId, Guid.NewGuid(), salespersonId, result, calculatedAtUtc);
        context.SaleCommissions.Add(commission);
        return commission;
    }

    private static GetCommissionsReportQueryHandler NewHandler(TestApplicationDbContext context, Guid callerId) =>
        new(context, TenantService(), CallerContext(callerId));

    [Fact]
    public async Task ReadOwnOnly_QueryingOwnSalespersonId_SeesOnlyOwnCommission()
    {
        using var context = CreateContext();
        Guid salespersonId = SeedUser(context, "Empleado", "commissions:read-own");
        Guid otherSalespersonId = SeedUser(context, "Empleado", "commissions:read-own");
        var from = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc);
        SeedCommission(context, salespersonId, 1_000m, from.AddDays(10));
        SeedCommission(context, otherSalespersonId, 2_000m, from.AddDays(11));
        await context.SaveChangesAsync();

        Result<IReadOnlyList<CommissionReportRow>> result = await NewHandler(context, salespersonId)
            .Handle(new GetCommissionsReportQuery(from, to, salespersonId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
        result.Value![0].SalespersonId.Should().Be(salespersonId);
        result.Value[0].Amount.Should().Be(1_000m);
    }

    [Fact]
    public async Task ReadOwnOnly_RequestingAnotherSalespersonId_IsRejected()
    {
        using var context = CreateContext();
        Guid salespersonId = SeedUser(context, "Empleado", "commissions:read-own");
        Guid otherSalespersonId = SeedUser(context, "Empleado", "commissions:read-own");
        var from = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc);
        SeedCommission(context, otherSalespersonId, 2_000m, from.AddDays(11));
        await context.SaveChangesAsync();

        Result<IReadOnlyList<CommissionReportRow>> result = await NewHandler(context, salespersonId)
            .Handle(new GetCommissionsReportQuery(from, to, otherSalespersonId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
    }

    [Fact]
    public async Task ReadAll_QueryingWithNoSalespersonId_SeesEveryonesCommissions()
    {
        using var context = CreateContext();
        Guid adminId = SeedUser(context, "Admin", "commissions:read-own", "commissions:read-all", "commissions:manage");
        Guid salespersonA = SeedUser(context, "Empleado", "commissions:read-own");
        Guid salespersonB = SeedUser(context, "Empleado", "commissions:read-own");
        var from = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc);
        SeedCommission(context, salespersonA, 1_000m, from.AddDays(10));
        SeedCommission(context, salespersonB, 2_000m, from.AddDays(11));
        await context.SaveChangesAsync();

        Result<IReadOnlyList<CommissionReportRow>> result = await NewHandler(context, adminId)
            .Handle(new GetCommissionsReportQuery(from, to, null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value!.Select(r => r.SalespersonId).Should().BeEquivalentTo([salespersonA, salespersonB]);
    }

    [Fact]
    public async Task ReadAll_RequestingASpecificSalespersonId_IsNotForcedToOwn()
    {
        using var context = CreateContext();
        Guid adminId = SeedUser(context, "Admin", "commissions:read-own", "commissions:read-all", "commissions:manage");
        Guid salespersonA = SeedUser(context, "Empleado", "commissions:read-own");
        var from = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc);
        SeedCommission(context, salespersonA, 1_000m, from.AddDays(10));
        await context.SaveChangesAsync();

        Result<IReadOnlyList<CommissionReportRow>> result = await NewHandler(context, adminId)
            .Handle(new GetCommissionsReportQuery(from, to, salespersonA), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
        result.Value![0].SalespersonId.Should().Be(salespersonA);
    }
}

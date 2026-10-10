using Application.Abstractions.Authentication;
using Application.Abstractions.Storage;
using Application.Abstractions.Tenancy;
using Application.Cars.GetAll;
using Application.Clients.GetAll;
using Application.Common;
using Application.Reports.GetFinancialReport;
using Application.UnitTests;
using Domain.Clients;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace ApplicationTests.Common;

/// <summary>Tope de pageSize (100) y rangos de fecha acotados (366 días).</summary>
public class PaginationTests
{
    private static readonly Guid Dealer = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private static TestApplicationDbContext CreateContext(Guid? dealerId = null)
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return dealerId is null ? new TestApplicationDbContext(options) : new TestApplicationDbContext(options, dealerId.Value);
    }

    [Theory]
    [InlineData(100000, 100)]
    [InlineData(101, 100)]
    [InlineData(100, 100)]
    [InlineData(20, 20)]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    public void ClampPageSize_BoundsTheValue(int requested, int expected) =>
        Pagination.ClampPageSize(requested).Should().Be(expected);

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-3, 1)]
    [InlineData(7, 7)]
    public void ClampPage_NeverGoesBelowOne(int requested, int expected) =>
        Pagination.ClampPage(requested).Should().Be(expected);

    [Fact]
    public void ClampRange_TenYears_IsCutToMaxDays()
    {
        var from = new DateTime(2016, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var (clampedFrom, clampedTo) = Pagination.ClampRange(from, to);

        clampedFrom.Should().Be(from);
        (clampedTo - clampedFrom).TotalDays.Should().Be(Pagination.MaxDateRangeDays);
    }

    [Fact]
    public void ClampRange_ShortRange_IsUntouched()
    {
        var from = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = from.AddDays(30);

        Pagination.ClampRange(from, to).Should().Be((from, to));
    }

    [Fact]
    public async Task GetAllCars_PageSizeOneHundredThousand_IsClampedToTheMaximum()
    {
        using var context = CreateContext();
        var handler = new GetAllCarsQueryHandler(context, new Mock<IStorageService>().Object, new Mock<IUserContext>().Object);

        var result = await handler.Handle(new GetAllCarsQuery(PageSize: 100000), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.PageSize.Should().Be(Pagination.MaxPageSize);
    }

    [Fact]
    public async Task GetAllClients_PageSizeOneHundredThousand_IsClampedToTheMaximum()
    {
        using var context = CreateContext();
        var handler = new GetAllClientsQueryHandler(context);

        var result = await handler.Handle(new GetAllClientsQuery(PageSize: 100000), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.PageSize.Should().Be(Pagination.MaxPageSize);
    }

    [Fact]
    public async Task FinancialReport_TenYearRange_IsClampedToMaxDays()
    {
        using var context = CreateContext(Dealer);
        var tenant = new Mock<ICurrentTenantService>();
        tenant.SetupGet(t => t.HasTenant).Returns(true);
        tenant.SetupGet(t => t.DealerId).Returns(Dealer);
        var handler = new GetFinancialReportQueryHandler(context, tenant.Object);
        var from = new DateTime(2016, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var result = await handler.Handle(
            new GetFinancialReportQuery(from, from.AddYears(10), ReportGroupBy.Day),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (result.Value.To - result.Value.From).TotalDays.Should().BeLessThanOrEqualTo(Pagination.MaxDateRangeDays);
        result.Value.Series.Count.Should().BeLessThanOrEqualTo(Pagination.MaxDateRangeDays + 1);
    }
}

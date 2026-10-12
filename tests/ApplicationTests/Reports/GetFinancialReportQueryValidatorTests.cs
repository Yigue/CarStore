using Application.Reports;
using Application.Reports.GetFinancialReport;

namespace ApplicationTests.Reports;

public class GetFinancialReportQueryValidatorTests
{
    private readonly GetFinancialReportQueryValidator _validator = new();

    private static GetFinancialReportQuery Query(DateTime from, DateTime to) =>
        new(from, to, ReportGroupBy.Month);

    [Fact]
    public void Validate_ShouldFail_WhenRangeIsMissing()
    {
        var result = _validator.Validate(Query(default, default));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorCode == ReportRangeErrors.RangeInvalid);
    }

    [Fact]
    public void Validate_ShouldFail_WhenRangeExceeds366Days()
    {
        var from = new DateTime(2050, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var result = _validator.Validate(Query(from, from.AddDays(367)));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorCode == "REPORT_RANGE_INVALID");
    }

    [Fact]
    public void Validate_ShouldFail_WhenFromIsAfterTo()
    {
        var from = new DateTime(2050, 6, 10, 0, 0, 0, DateTimeKind.Utc);

        var result = _validator.Validate(Query(from, from.AddDays(-1)));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorCode == "REPORT_RANGE_INVALID");
    }

    [Fact]
    public void Validate_ShouldPass_ForExactly366Days()
    {
        var from = new DateTime(2050, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        _validator.Validate(Query(from, from.AddDays(366))).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ShouldPass_ForSingleDay()
    {
        var from = new DateTime(2050, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        _validator.Validate(Query(from, from)).IsValid.Should().BeTrue();
    }
}

public class ReportValidatorTests
{
    private sealed record FakeReportQuery(DateTime From, DateTime To) : IReportRangeQuery;

    private sealed class FakeValidator : ReportRangeValidator<FakeReportQuery>;

    [Fact]
    public void SharedValidator_AppliesRangeRule_ToAnyReportQuery()
    {
        var from = new DateTime(2050, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        new FakeValidator().Validate(new FakeReportQuery(from, from.AddDays(400)))
            .Errors.Should().Contain(e => e.ErrorCode == "REPORT_RANGE_INVALID");
    }

    [Fact]
    public void AllReportQueries_ImplementSharedRangeContract()
    {
        var reportQueries = typeof(GetFinancialReportQuery).Assembly.GetTypes()
            .Where(t => t.Namespace is { } ns && ns.StartsWith("Application.Reports") && t.Name.EndsWith("Query"));

        reportQueries.Should().OnlyContain(t => typeof(IReportRangeQuery).IsAssignableFrom(t));
    }
}

using FluentValidation;

namespace Application.Reports;

public static class ReportRangeErrors
{
    public const string RangeInvalid = "REPORT_RANGE_INVALID";

    public const int MaxRangeDays = 366;
}

/// <summary>
/// Shared range rule for report queries: both dates present, from &lt;= to, and at most 366 days.
/// </summary>
public abstract class ReportRangeValidator<TQuery> : AbstractValidator<TQuery>
    where TQuery : IReportRangeQuery
{
    protected ReportRangeValidator()
    {
        RuleFor(x => x)
            .Must(q => q.From != default && q.To != default && q.From <= q.To)
            .WithErrorCode(ReportRangeErrors.RangeInvalid)
            .WithMessage("'from' and 'to' are required and 'from' must not be after 'to'.");

        RuleFor(x => x)
            .Must(q => q.From == default || q.To == default || q.From > q.To
                || (q.To - q.From).TotalDays <= ReportRangeErrors.MaxRangeDays)
            .WithErrorCode(ReportRangeErrors.RangeInvalid)
            .WithMessage($"The report range cannot exceed {ReportRangeErrors.MaxRangeDays} days.");
    }
}

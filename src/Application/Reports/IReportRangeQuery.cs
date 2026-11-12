namespace Application.Reports;

/// <summary>
/// Contract shared by every query under Reports/*: they all require a bounded date range (RN-026).
/// </summary>
public interface IReportRangeQuery
{
    DateTime From { get; }

    DateTime To { get; }
}

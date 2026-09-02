using System;
using Application.Platform.AuditLogs.GetPlatformAuditLogs;
using FluentAssertions;
using Xunit;

namespace Application.UnitTests.Platform.AuditLogs;

public class GetPlatformAuditLogsQueryValidatorTests
{
    [Fact]
    public void Validator_RejectsPageSizeAbove100_AndInvertedDateRange()
    {
        var validator = new GetPlatformAuditLogsQueryValidator();

        var invalidPageSize = new GetPlatformAuditLogsQuery(PageSize: 101);
        var res1 = validator.Validate(invalidPageSize);
        res1.IsValid.Should().BeFalse();
        res1.Errors.Should().Contain(e => e.PropertyName == nameof(GetPlatformAuditLogsQuery.PageSize));

        var invertedDates = new GetPlatformAuditLogsQuery(
            FromUtc: DateTime.UtcNow,
            ToUtc: DateTime.UtcNow.AddHours(-1));
        var res2 = validator.Validate(invertedDates);
        res2.IsValid.Should().BeFalse();
    }
}

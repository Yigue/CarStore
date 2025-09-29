using System;
using SharedKernel;

namespace Application.UnitTests;

internal sealed class FakeDateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow { get; set; } = new DateTime(2024, 1, 1);
}

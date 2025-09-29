using Infrastructure.Time;

namespace InfrastructureTests.Time;

public class DateTimeProviderTests
{
    [Fact]
    public void UtcNow_ReturnsCurrentTime()
    {
        var provider = new DateTimeProvider();
        var before = DateTime.UtcNow;
        var actual = provider.UtcNow;
        var after = DateTime.UtcNow;

        actual.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }
}

using Application.Tenancy.Purge;
using FluentAssertions;
using Infrastructure.BackgroundJobs;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Quartz;
using SharedKernel;

namespace InfrastructureTests.Tenancy;

/// <summary>
/// RN-013 (A1.4): the purge job is OFF by default. Without <c>Retention:PurgeEnabled=true</c> it only does a dry run
/// (counts and logs, deletes nothing). Only the explicit flag turns it into a real purge.
/// </summary>
public class PurgeTenantDataJobTests
{
    private static async Task<PurgeDueTenantsCommand> RunAsync(Dictionary<string, string?>? config)
    {
        PurgeDueTenantsCommand? sent = null;
        var sender = new Mock<ISender>();
        sender
            .Setup(s => s.Send(It.IsAny<IRequest<Result<PurgeRunSummary>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IRequest<Result<PurgeRunSummary>> r, CancellationToken _) =>
            {
                sent = (PurgeDueTenantsCommand)r;
                return Result.Success(new PurgeRunSummary(sent.DryRun, 0, 0, 0, 0));
            });
        var job = new PurgeTenantDataJob(
            sender.Object,
            new ConfigurationBuilder().AddInMemoryCollection(config ?? new Dictionary<string, string?>()).Build(),
            NullLogger<PurgeTenantDataJob>.Instance);
        var context = new Mock<IJobExecutionContext>();
        context.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);

        await job.Execute(context.Object);

        sent.Should().NotBeNull();
        return sent!;
    }

    [Fact]
    public async Task ByDefault_RunsAsDryRun()
    {
        (await RunAsync(null)).DryRun.Should().BeTrue();
    }

    [Theory]
    [InlineData("false")]
    [InlineData("")]
    [InlineData("yes please")]
    public async Task WhenFlagIsNotExactlyTrue_RunsAsDryRun(string value)
    {
        (await RunAsync(new() { ["Retention:PurgeEnabled"] = value })).DryRun.Should().BeTrue();
    }

    [Fact]
    public async Task WhenFlagIsTrue_RunsForReal()
    {
        (await RunAsync(new() { ["Retention:PurgeEnabled"] = "true" })).DryRun.Should().BeFalse();
    }
}

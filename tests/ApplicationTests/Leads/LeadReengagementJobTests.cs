using Application.Abstractions.Messaging;
using Application.UnitTests;
using Domain.Leads;
using Infrastructure.BackgroundJobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Quartz;

namespace Application.UnitTests.Leads;

/// <summary>
/// Lost-lead re-engagement (priority item 3): selection query logic, one-shot semantics,
/// and the disabled-by-default flag.
/// </summary>
public class LeadReengagementJobTests
{
    private static readonly Guid DealerId = Guid.NewGuid();

    private TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new TestApplicationDbContext(options);
        context.DealerSettings.Add(new Domain.DealerSettings.DealerSettings(DealerId, "Test Dealer", "dealer@test.com"));
        context.SaveChanges();
        return context;
    }

    private static LeadReengagementJob CreateJob(
        TestApplicationDbContext context,
        IEmailService emailService,
        FakeDateTimeProvider dateTimeProvider,
        bool enabled = true,
        int daysAfterLost = 30) =>
        new(
            context,
            emailService,
            Options.Create(new CrmReengagementOptions { Enabled = enabled, DaysAfterLost = daysAfterLost }),
            dateTimeProvider,
            NullLogger<LeadReengagementJob>.Instance);

    private Lead CreateLostLead(TestApplicationDbContext context, DateTime createdAt)
    {
        var lead = Lead.Create(DealerId, "Old Lead", "lost@test.com", "555-0000", LeadSource.Web, createdAt);
        lead.UpdateStatus(LeadStatus.Perdido, null, LeadLossReason.Precio);
        context.Leads.Add(lead);
        context.SaveChanges();
        return lead;
    }

    [Fact]
    public async Task Execute_ShouldSendNothing_WhenDisabled()
    {
        var now = DateTime.UtcNow;
        var context = CreateContext();
        CreateLostLead(context, now.AddDays(-60));

        var emailService = new Mock<IEmailService>();
        var job = CreateJob(context, emailService.Object, new FakeDateTimeProvider { UtcNow = now }, enabled: false);

        await job.Execute(new Mock<IJobExecutionContext>().Object);

        emailService.Verify(
            e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Execute_ShouldSendEmail_AndStampTimestamp_ForEligibleLostLead()
    {
        var now = DateTime.UtcNow;
        var context = CreateContext();
        var lead = CreateLostLead(context, now.AddDays(-60));

        var emailService = new Mock<IEmailService>();
        emailService
            .Setup(e => e.SendEmailAsync(lead.Email.Value, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var job = CreateJob(context, emailService.Object, new FakeDateTimeProvider { UtcNow = now });

        await job.Execute(new Mock<IJobExecutionContext>().Object);

        emailService.Verify(
            e => e.SendEmailAsync(lead.Email.Value, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);

        var updated = await context.Leads.IgnoreQueryFilters().FirstAsync(l => l.Id == lead.Id);
        updated.ReengagementSentAtUtc.Should().Be(now);
    }

    [Fact]
    public async Task Execute_ShouldSkip_LeadsNotOldEnough()
    {
        var now = DateTime.UtcNow;
        var context = CreateContext();
        var lead = CreateLostLead(context, now.AddDays(-5)); // younger than the 30-day default

        var emailService = new Mock<IEmailService>();
        var job = CreateJob(context, emailService.Object, new FakeDateTimeProvider { UtcNow = now });

        await job.Execute(new Mock<IJobExecutionContext>().Object);

        emailService.Verify(
            e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);

        var updated = await context.Leads.IgnoreQueryFilters().FirstAsync(l => l.Id == lead.Id);
        updated.ReengagementSentAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task Execute_ShouldSkip_LeadsAlreadyReengaged()
    {
        var now = DateTime.UtcNow;
        var context = CreateContext();
        var lead = CreateLostLead(context, now.AddDays(-60));
        lead.MarkReengagementSent(now.AddDays(-1));
        context.SaveChanges();

        var emailService = new Mock<IEmailService>();
        var job = CreateJob(context, emailService.Object, new FakeDateTimeProvider { UtcNow = now });

        await job.Execute(new Mock<IJobExecutionContext>().Object);

        emailService.Verify(
            e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Execute_ShouldSkip_LeadsNotInPerdidoStatus()
    {
        var now = DateTime.UtcNow;
        var context = CreateContext();
        var lead = Lead.Create(DealerId, "Active Lead", "active@test.com", "555-1111", LeadSource.Web, now.AddDays(-60));
        context.Leads.Add(lead);
        context.SaveChanges();

        var emailService = new Mock<IEmailService>();
        var job = CreateJob(context, emailService.Object, new FakeDateTimeProvider { UtcNow = now });

        await job.Execute(new Mock<IJobExecutionContext>().Object);

        emailService.Verify(
            e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Execute_ShouldLeaveTimestampNull_WhenEmailSendThrows()
    {
        var now = DateTime.UtcNow;
        var context = CreateContext();
        var lead = CreateLostLead(context, now.AddDays(-60));

        var emailService = new Mock<IEmailService>();
        emailService
            .Setup(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SMTP down"));

        var job = CreateJob(context, emailService.Object, new FakeDateTimeProvider { UtcNow = now });

        var act = async () => await job.Execute(new Mock<IJobExecutionContext>().Object);
        await act.Should().NotThrowAsync("a transient SMTP failure must not crash the job");

        var updated = await context.Leads.IgnoreQueryFilters().FirstAsync(l => l.Id == lead.Id);
        updated.ReengagementSentAtUtc.Should().BeNull("so the lead is retried on the next run instead of losing the one-shot outreach");
    }
}

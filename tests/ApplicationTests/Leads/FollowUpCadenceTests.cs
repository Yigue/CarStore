using Application.Leads.FollowUp;
using Domain.Leads;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Application.UnitTests.Leads;

/// <summary>
/// sfc B6.2 / RN-041: an open lead with no activity for the dealer's cadence (3 days by default) gets one follow-up
/// reminder for its assigned salesperson; recent activity, a closed lead or a reminder already pending produce nothing.
/// </summary>
public class FollowUpCadenceTests
{
    private static readonly Guid DealerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid AgentId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly DateTime Now = new(2026, 10, 10, 9, 0, 0, DateTimeKind.Utc);

    // No dealer on the context: the sweep runs platform-wide from Quartz.
    private static TestApplicationDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<TestApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static GenerateFollowUpRemindersCommandHandler Handler(TestApplicationDbContext context) =>
        new(context, new FakeDateTimeProvider { UtcNow = Now });

    private static async Task<Lead> SeedLeadAsync(TestApplicationDbContext context, LeadStatus status, DateTime createdAt)
    {
        if (!await context.DealerSettings.AnyAsync())
            context.DealerSettings.Add(new Domain.DealerSettings.DealerSettings(DealerId, "Autos Sur", "sur@dealer.com"));
        var lead = Lead.Create(DealerId, "Ana", $"{Guid.NewGuid():N}@x.com", "1", LeadSource.Web, createdAt);
        lead.AssignAgent(AgentId);
        if (status != LeadStatus.Nuevo)
            lead.ForceStatus(status);
        context.Leads.Add(lead);
        await context.SaveChangesAsync();
        return lead;
    }

    private static Task<List<LeadActivity>> RemindersAsync(TestApplicationDbContext context, Guid leadId) =>
        context.LeadActivities.Where(a => a.LeadId == leadId && a.Type == LeadActivityType.FollowUpReminder).ToListAsync();

    [Fact]
    public async Task A_quiet_open_lead_gets_one_reminder_for_its_salesperson()
    {
        using var context = CreateContext();
        var lead = await SeedLeadAsync(context, LeadStatus.Contactado, Now.AddDays(-4));

        var result = await Handler(context).Handle(new GenerateFollowUpRemindersCommand(), CancellationToken.None);

        result.Value.Should().Be(1);
        var reminder = (await RemindersAsync(context, lead.Id)).Should().ContainSingle().Subject;
        reminder.RelatedEntityId.Should().Be(AgentId);
        reminder.OccurredAtUtc.Should().Be(Now);
    }

    [Fact]
    public async Task A_lead_with_activity_today_gets_nothing()
    {
        using var context = CreateContext();
        var lead = await SeedLeadAsync(context, LeadStatus.Contactado, Now.AddDays(-10));
        context.LeadActivities.Add(LeadActivity.Record(DealerId, lead.Id, LeadActivityType.NoteAdded, "llamado", Now.AddHours(-2)));
        await context.SaveChangesAsync();

        var result = await Handler(context).Handle(new GenerateFollowUpRemindersCommand(), CancellationToken.None);

        result.Value.Should().Be(0);
        (await RemindersAsync(context, lead.Id)).Should().BeEmpty();
    }

    [Fact]
    public async Task Running_again_does_not_duplicate_a_pending_reminder()
    {
        using var context = CreateContext();
        var lead = await SeedLeadAsync(context, LeadStatus.Negociacion, Now.AddDays(-5));

        await Handler(context).Handle(new GenerateFollowUpRemindersCommand(), CancellationToken.None);
        var second = await new GenerateFollowUpRemindersCommandHandler(context, new FakeDateTimeProvider { UtcNow = Now.AddDays(4) })
            .Handle(new GenerateFollowUpRemindersCommand(), CancellationToken.None);

        second.Value.Should().Be(0);
        (await RemindersAsync(context, lead.Id)).Should().ContainSingle();
    }

    [Theory]
    [InlineData(LeadStatus.Ganado)]
    [InlineData(LeadStatus.Perdido)]
    [InlineData(LeadStatus.Archivado)]
    public async Task Closed_leads_get_nothing(LeadStatus status)
    {
        using var context = CreateContext();
        var lead = await SeedLeadAsync(context, status, Now.AddDays(-30));

        await Handler(context).Handle(new GenerateFollowUpRemindersCommand(), CancellationToken.None);

        (await RemindersAsync(context, lead.Id)).Should().BeEmpty();
    }

    [Fact]
    public async Task A_dealer_with_cadence_off_gets_nothing()
    {
        using var context = CreateContext();
        var lead = await SeedLeadAsync(context, LeadStatus.Contactado, Now.AddDays(-30));
        (await context.DealerSettings.SingleAsync()).SetFollowUpCadenceDays(0).IsSuccess.Should().BeTrue();
        await context.SaveChangesAsync();

        await Handler(context).Handle(new GenerateFollowUpRemindersCommand(), CancellationToken.None);

        (await RemindersAsync(context, lead.Id)).Should().BeEmpty();
    }
}

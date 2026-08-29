using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Leads.GetActivity;
using Domain.Leads;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Application.UnitTests.Leads;

public class GetLeadActivityQueryHandlerTests
{
    private static readonly Guid DealerId = Guid.NewGuid();

    private static TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestApplicationDbContext(options);
    }

    private static async Task<Lead> SeedLeadAsync(TestApplicationDbContext context)
    {
        var lead = Lead.Create(
            DealerId, "Ana Fernandez", "ana@test.com", "1", LeadSource.Web, DateTime.UtcNow);
        context.Leads.Add(lead);
        await context.SaveChangesAsync();
        return lead;
    }

    private static LeadActivity Activity(Guid leadId, string description, DateTime at) =>
        LeadActivity.Record(DealerId, leadId, LeadActivityType.StatusChanged, description, at);

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenTheLeadDoesNotExist()
    {
        using var context = CreateContext();

        var result = await new GetLeadActivityQueryHandler(context)
            .Handle(new GetLeadActivityQuery(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_Should_ReturnNewestFirst()
    {
        using var context = CreateContext();
        Lead lead = await SeedLeadAsync(context);

        var day = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        context.LeadActivities.AddRange(
            Activity(lead.Id, "primera", day),
            Activity(lead.Id, "segunda", day.AddHours(1)),
            Activity(lead.Id, "tercera", day.AddHours(2)));
        await context.SaveChangesAsync();

        var result = await new GetLeadActivityQueryHandler(context)
            .Handle(new GetLeadActivityQuery(lead.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCount.Should().Be(3);
        result.Value.Items.Select(i => i.Description)
            .Should().ContainInOrder("tercera", "segunda", "primera");
    }

    [Fact]
    public async Task Handle_Should_ReturnOnlyThisLeadsHistory()
    {
        using var context = CreateContext();
        Lead lead = await SeedLeadAsync(context);
        Lead other = await SeedLeadAsync(context);

        context.LeadActivities.AddRange(
            Activity(lead.Id, "de este lead", DateTime.UtcNow),
            Activity(other.Id, "del otro lead", DateTime.UtcNow));
        await context.SaveChangesAsync();

        var result = await new GetLeadActivityQueryHandler(context)
            .Handle(new GetLeadActivityQuery(lead.Id), CancellationToken.None);

        result.Value.Items.Should().ContainSingle()
            .Which.Description.Should().Be("de este lead");
    }

    [Fact]
    public async Task Handle_Should_Paginate_AndStillReportTheFullTotal()
    {
        using var context = CreateContext();
        Lead lead = await SeedLeadAsync(context);

        var day = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        for (int i = 0; i < 5; i++)
        {
            context.LeadActivities.Add(Activity(lead.Id, $"entrada {i}", day.AddHours(i)));
        }
        await context.SaveChangesAsync();

        var result = await new GetLeadActivityQueryHandler(context)
            .Handle(new GetLeadActivityQuery(lead.Id, Page: 2, PageSize: 2), CancellationToken.None);

        result.Value.TotalCount.Should().Be(5, "the count is of the whole history, not the page");
        result.Value.Items.Should().HaveCount(2);
        result.Value.Items.Select(i => i.Description).Should().ContainInOrder("entrada 2", "entrada 1");
    }

    [Fact]
    public async Task Handle_Should_CarryTheRelatedReference_SoTheUiCanLinkIt()
    {
        using var context = CreateContext();
        Lead lead = await SeedLeadAsync(context);
        var quoteId = Guid.NewGuid();

        context.LeadActivities.Add(LeadActivity.Record(
            DealerId, lead.Id, LeadActivityType.QuoteCreated, "Cotización creada", DateTime.UtcNow,
            relatedEntityId: quoteId, relatedEntityType: "Quote"));
        await context.SaveChangesAsync();

        var result = await new GetLeadActivityQueryHandler(context)
            .Handle(new GetLeadActivityQuery(lead.Id), CancellationToken.None);

        LeadActivityEntry entry = result.Value.Items.Single();
        entry.RelatedEntityId.Should().Be(quoteId);
        entry.RelatedEntityType.Should().Be("Quote");
    }
}

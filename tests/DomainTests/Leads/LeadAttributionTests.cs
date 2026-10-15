using Domain.Leads;
using FluentAssertions;

namespace DomainTests.Leads;

/// <summary>sfc C0.1: attribution comes from an anonymous visitor, so values are trimmed, blanks dropped and lengths capped.</summary>
public class LeadAttributionTests
{
    [Fact]
    public void Create_Should_TrimCapAndDropBlankValues()
    {
        var touch = LeadAttribution.Create("  google  ", "   ", new string('x', 500), null, null, null, "/", null, null, DateTime.UtcNow);

        touch.UtmSource.Should().Be("google");
        touch.UtmMedium.Should().BeNull();
        touch.UtmCampaign.Should().HaveLength(LeadAttribution.MaxLength);
    }

    [Fact]
    public void SetFirstTouch_Should_NeverOverwriteTheFirstOne()
    {
        var lead = Lead.Create(Guid.NewGuid(), "Ana", "ana@x.com", "1", LeadSource.Web, DateTime.UtcNow);
        var first = LeadAttribution.Create("google", null, null, null, null, null, "/", null, null, DateTime.UtcNow);

        lead.SetFirstTouch(first);
        lead.SetFirstTouch(LeadAttribution.Create("facebook", null, null, null, null, null, "/", null, null, DateTime.UtcNow));

        lead.Attribution.Should().Be(first);
    }
}

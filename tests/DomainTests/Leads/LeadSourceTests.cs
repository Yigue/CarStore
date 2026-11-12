using Domain.Leads;
using FluentAssertions;

namespace DomainTests.Leads;

public class LeadSourceTests
{
    [Theory]
    [InlineData(LeadSource.WhatsApp, "WhatsApp")]
    [InlineData(LeadSource.MercadoLibre, "MercadoLibre")]
    [InlineData(LeadSource.Facebook, "Facebook")]
    [InlineData(LeadSource.Instagram, "Instagram")]
    [InlineData(LeadSource.Google, "Google")]
    public void New_sources_are_persisted_by_name(LeadSource source, string expectedName)
    {
        source.ToString().Should().Be(expectedName);
        Enum.TryParse<LeadSource>(expectedName, out var parsed).Should().BeTrue();
        parsed.Should().Be(source);
    }

    [Fact]
    public void Existing_sources_keep_their_names()
    {
        Enum.GetNames<LeadSource>().Should().Contain(new[] { "Web", "Portal", "Referral", "Otro" });
    }

    [Fact]
    public void Every_source_name_fits_the_30_char_column()
    {
        Enum.GetNames<LeadSource>().Should().OnlyContain(name => name.Length <= 30);
    }
}

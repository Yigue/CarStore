using Domain.DealerSettings;
using FluentAssertions;
using DealerSettingsEntity = Domain.DealerSettings.DealerSettings;

namespace DomainTests.DealerSettings;

/// <summary>
/// PR1 BE TDD RED→GREEN: DealerSettings tenant-identity invariants.
///
/// Locks the rules added by saas-custom-domains PR1:
/// - DealerSettings exposes Slug (required, ≤63 chars) + IsActive (default true).
/// - ChangeSlug(newSlug, newHostName) enforces RFC 1035 (lowercase + alnum + hyphens,
///   ASCII only, no leading/trailing hyphen, 1–63 chars).
/// - Invalid input raises DomainException — no silent coercion.
/// </summary>
public class DealerSettingsTenantIdentityTests
{
    private const string ValidDealerId = "11111111-1111-1111-1111-111111111111";

    private static DealerSettingsEntity BuildSettings(string dealerName = "Lux Dealership") =>
        new(
            Guid.Parse(ValidDealerId),
            dealerName,
            "info@example.com");

    // ── 1.1.1 — Slug + IsActive properties exist with correct defaults ─────────

    [Fact]
    public void Newly_Created_DealerSettings_Should_Default_IsActive_To_True()
    {
        var settings = BuildSettings();

        settings.IsActive.Should().BeTrue("a freshly created dealer is active by default");
    }

    [Fact]
    public void ChangeSlug_WithValidValues_Should_SetBothSlugAndHostName()
    {
        var settings = BuildSettings();

        settings.ChangeSlug("lux-dealership", "lux.carstore.com");

        settings.Slug.Should().Be("lux-dealership");
        settings.HostName.Should().Be("lux.carstore.com");
    }

    // ── 1.1.2 — RFC 1035 validation on Slug + HostName ────────────────────────

    [Theory]
    [InlineData("lux-dealership", "lux.carstore.com")]
    [InlineData("abc", "abc.carstore.com")]
    [InlineData("a-1-2-3", "a-1-2-3.carstore.com")]
    public void ChangeSlug_WithValidRfc1035Values_Should_Succeed(string slug, string hostName)
    {
        var settings = BuildSettings();

        var act = () => settings.ChangeSlug(slug, hostName);

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("Lux-Dealership", "lux.carstore.com", "uppercase letter")]
    [InlineData("lux_dealership", "lux.carstore.com", "underscore not allowed")]
    [InlineData("lux--dealership", "lux.carstore.com", "consecutive hyphens")]
    [InlineData("-lux", "lux.carstore.com", "leading hyphen")]
    [InlineData("lux-", "lux.carstore.com", "trailing hyphen")]
    [InlineData("", "lux.carstore.com", "empty")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "lux.carstore.com", "64 chars exceeds 63")]
    [InlineData("lux deal ership", "lux.carstore.com", "contains space")]
    [InlineData("lux-dealership", "LUX.carstore.com", "uppercase in hostname")]
    [InlineData("lux-dealership", "lux_carstore.com", "underscore in hostname")]
    public void ChangeSlug_WithInvalidSlugOrHostname_Should_ThrowDomainException(
        string slug, string hostName, string scenario)
    {
        var settings = BuildSettings();

        var act = () => settings.ChangeSlug(slug, hostName);

        act.Should().Throw<DomainException>(because: scenario);
    }
}

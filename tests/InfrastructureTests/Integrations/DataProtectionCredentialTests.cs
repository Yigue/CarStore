using Application.Abstractions.Integrations;
using FluentAssertions;
using Infrastructure.Integrations;
using Microsoft.AspNetCore.DataProtection;
using Xunit;

namespace InfrastructureTests.Integrations;

/// <summary>sfc C1.1 / ADR C22: credentials are protected with purpose integrations.credentials.v1.</summary>
public class DataProtectionCredentialTests
{
    private const string Secret = "APP_USR-super-secret-refresh-token";

    [Fact]
    public void Protect_ThenUnprotect_RoundTrips_AndHidesThePlaintext()
    {
        var provider = new EphemeralDataProtectionProvider();
        var protector = new DataProtectionCredentialProtector(provider);

        string protectedValue = protector.Protect(Secret);

        protectedValue.Should().NotContain(Secret);
        protectedValue.Should().NotBe(Secret);
        protector.Unprotect(protectedValue).Should().Be(Secret);
    }

    [Fact]
    public void Unprotect_WithAnotherPurpose_Fails()
    {
        var provider = new EphemeralDataProtectionProvider();
        var protector = new DataProtectionCredentialProtector(provider);
        string protectedValue = protector.Protect(Secret);

        Action act = () => provider.CreateProtector("some.other.purpose").Unprotect(protectedValue);

        act.Should().Throw<System.Security.Cryptography.CryptographicException>();
    }

    [Fact]
    public void Purpose_IsVersioned()
    {
        ICredentialProtector.Purpose.Should().Be("integrations.credentials.v1");
    }
}

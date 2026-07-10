using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace ArchitectureTests;

public class StripeIsolationTests : BaseTest
{
    [Fact]
    public void DomainLayer_ShouldNotHaveDependencyOn_Stripe()
    {
        TestResult result = Types.InAssembly(DomainAssembly)
            .Should()
            .NotHaveDependencyOn("Stripe")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void ApplicationLayer_ShouldNotHaveDependencyOn_Stripe()
    {
        TestResult result = Types.InAssembly(ApplicationAssembly)
            .Should()
            .NotHaveDependencyOn("Stripe")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }
}

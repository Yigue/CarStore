using FluentAssertions;
using NetArchTest.Rules;

namespace ArchitectureTests;

/// <summary>
/// Architecture tests for Application.Platform handlers (task 1.5.6).
/// Guards that platform handlers:
/// - Do not depend on Application.Common (wrong namespace)
/// - Do not depend on Application.Abstractions.Tenancy (handlers use IgnoreQueryFilters, not tenant service)
/// </summary>
public class PlatformHandlersTests : BaseTest
{
    [Fact]
    public void PlatformTypes_ShouldNotDependOn_ApplicationCommonNamespace()
    {
        // Application.Common is a forbidden namespace (common platform code lives in
        // Application.Platform.Common). This prevents accidental wrong imports.
        var result = Types.InAssembly(ApplicationAssembly)
            .That()
            .ResideInNamespace("Application.Platform")
            .Should()
            .NotHaveDependencyOn("Application.Common")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Platform handlers must not import Application.Common — use Application.Platform.Common instead.");
    }

    [Fact]
    public void PlatformHandlers_ShouldNotDependOn_TenancyAbstractions()
    {
        // Platform handlers bypass tenant filtering via IgnoreQueryFilters().
        // They must NOT inject ICurrentTenantService — doing so would introduce
        // a tenant-coupled code path where none should exist (ADR-2).
        var result = Types.InAssembly(ApplicationAssembly)
            .That()
            .ResideInNamespace("Application.Platform")
            .And()
            .HaveNameEndingWith("Handler")
            .Should()
            .NotHaveDependencyOn("Application.Abstractions.Tenancy")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Platform handlers must use IgnoreQueryFilters(), not ICurrentTenantService.");
    }

    [Fact]
    public void PlatformHandlers_ShouldExist_InApplicationAssembly()
    {
        // Sanity check: at least some platform handler types exist.
        var handlers = Types.InAssembly(ApplicationAssembly)
            .That()
            .ResideInNamespace("Application.Platform")
            .And()
            .HaveNameEndingWith("Handler")
            .GetTypes();

        handlers.Should().NotBeEmpty("there must be at least one platform handler registered in the Application assembly.");
    }
}

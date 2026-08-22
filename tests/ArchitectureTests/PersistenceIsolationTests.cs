using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace ArchitectureTests;

public class PersistenceIsolationTests : BaseTest
{
    [Fact]
    public void ApplicationLayer_ShouldNotHaveDependencyOn_Npgsql_ExceptAllowlist()
    {
        TestResult result = Types.InAssembly(ApplicationAssembly)
            .That()
            .DoNotHaveName("DeleteCategoryCommandHandler")
            .Should()
            .NotHaveDependencyOn("Npgsql")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }
}

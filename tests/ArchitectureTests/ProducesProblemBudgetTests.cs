using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace ArchitectureTests;

public class ProducesProblemBudgetTests : BaseTest
{
    private const int FrozenBudget = 108;

    [Fact]
    public void ProducesProblem500_DeclarationCount_MatchesFrozenBudget()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "CleanArchitecture.sln")))
        {
            dir = dir.Parent;
        }

        dir.Should().NotBeNull("solution root must be found");
        var endpointsRoot = Path.Combine(dir!.FullName, "src", "Web.Api", "Endpoints");

        int count = 0;
        foreach (var file in Directory.EnumerateFiles(endpointsRoot, "*.cs", SearchOption.AllDirectories))
        {
            var source = File.ReadAllText(file);
            var tree = CSharpSyntaxTree.ParseText(source, path: file);
            var root = tree.GetCompilationUnitRoot();

            foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                var expression = invocation.Expression.ToString();
                if (expression.EndsWith("ProducesProblem", StringComparison.Ordinal) || expression.EndsWith(".ProducesProblem", StringComparison.Ordinal))
                {
                    var args = invocation.ArgumentList.Arguments;
                    if (args.Count > 0 && args[0].ToString().Contains("500"))
                    {
                        count++;
                    }
                }
            }
        }

        count.Should().Be(FrozenBudget, $"ProducesProblem(500) count should match the frozen budget of {FrozenBudget}. If the count drops below, update FrozenBudget to ratchet it down.");
    }
}

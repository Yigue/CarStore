using System.Text.RegularExpressions;
using Application.Abstractions.Data;
using FluentAssertions;
using Infrastructure.Database.Interceptors;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.EntityFrameworkCore;

namespace ArchitectureTests;

/// <summary>
/// sfc B2.2 / ADR C10 ratchet: <c>ExecuteUpdate</c>/<c>ExecuteDelete</c> go straight to SQL and bypass the change tracker, so
/// <c>TenantAuditInterceptor</c> never sees them. Changing an audited entity that way would leave no audit entry. Rule:
/// no raw bulk write over an audited entity (the allow-list lives in <see cref="TenantAuditInterceptor.AuditedEntityTypes"/>).
/// To bulk-write one, load and save through the change tracker (audited) instead.
/// </summary>
public class NoRawExecuteUpdateOnAuditedEntitiesTests
{
    private static readonly string[] BulkWriters =
        ["ExecuteUpdate", "ExecuteUpdateAsync", "ExecuteDelete", "ExecuteDeleteAsync"];

    private static string SolutionRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "CleanArchitecture.sln")))
            dir = dir.Parent;
        dir.Should().NotBeNull("the solution root must be reachable from the test output directory");
        return dir!.FullName;
    }

    /// <summary>DbSet property names on the app context whose element type is audited, e.g. Cars, Sales.</summary>
    private static HashSet<string> AuditedDbSetNames() =>
        typeof(IApplicationDbContext).GetProperties()
            .Where(p => p.PropertyType.IsGenericType
                && p.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>)
                && TenantAuditInterceptor.AuditedEntityTypes.Contains(p.PropertyType.GetGenericArguments()[0]))
            .Select(p => p.Name)
            .ToHashSet(StringComparer.Ordinal);

    private static IEnumerable<string> FindViolations(string source, string path, HashSet<string> dbSets, IEnumerable<string> typeNames)
    {
        var root = CSharpSyntaxTree.ParseText(source, path: path).GetCompilationUnitRoot();
        foreach (InvocationExpressionSyntax invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (invocation.Expression is not MemberAccessExpressionSyntax member
                || !BulkWriters.Contains(member.Name.Identifier.Text, StringComparer.Ordinal))
            {
                continue;
            }

            string receiver = member.Expression.ToString();
            bool audited = dbSets.Any(n => Regex.IsMatch(receiver, $@"\.{n}\b"))
                || typeNames.Any(t => Regex.IsMatch(receiver, $@"Set<\s*{t}\s*>"));
            if (audited)
            {
                yield return $"{Path.GetFileName(path)}: {member.Name.Identifier.Text} over an audited entity";
            }
        }
    }

    [Fact]
    public void AuditAllowList_CoversCar_Sale_AndDeposit()
    {
        TenantAuditInterceptor.AuditedEntityTypes.Should().Contain([typeof(Domain.Cars.Car), typeof(Domain.Sales.Sale), typeof(Domain.Deposits.Deposit)]);
        AuditedDbSetNames().Should().Contain(["Cars", "Sales"]);
    }

    [Fact]
    public void TheDetector_FlagsRawBulkWrites_OnAuditedEntities()
    {
        const string source = """
            class X { async Task Run(IApplicationDbContext context) {
              await context.Cars.Where(c => true).ExecuteUpdateAsync(s => s);
              await context.Sales.ExecuteDeleteAsync();
              await ((DbContext)context).Set<Car>().ExecuteDeleteAsync();
              await context.UserActionTokens.ExecuteUpdateAsync(s => s);
            } }
            """;

        var hits = FindViolations(source, "X.cs", AuditedDbSetNames(), TenantAuditInterceptor.AuditedEntityTypes.Select(t => t.Name)).ToList();

        hits.Should().HaveCount(3, "Cars, Sales and Set<Car>() are audited; UserActionTokens is not");
    }

    [Fact]
    public void Source_Should_NotBulkWrite_AuditedEntities()
    {
        var dbSets = AuditedDbSetNames();
        var typeNames = TenantAuditInterceptor.AuditedEntityTypes.Select(t => t.Name).ToList();
        string src = Path.Combine(SolutionRoot(), "src");

        var violations = Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .SelectMany(f => FindViolations(File.ReadAllText(f), f, dbSets, typeNames))
            .ToList();

        violations.Should().BeEmpty(
            "ExecuteUpdate/ExecuteDelete bypass TenantAuditInterceptor; change audited entities (Car, Sale, Deposit) through the change tracker");
    }
}

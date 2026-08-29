using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace ArchitectureTests;

/// <summary>
/// Domain event handlers run without a tenant, and nothing in a handler's own source says so.
///
/// <para>
/// <c>ApplicationDbContext.SaveChangesAsync</c> writes domain events to the outbox rather than
/// publishing them in-process; <c>ProcessOutboxMessagesJob</c> dispatches them later with no HTTP
/// context. <c>CurrentTenantService.HasTenant</c> is therefore <b>false</b> for the whole of a
/// handler's execution, and every global query filter reads
/// <c>!HasTenant || DealerId == ...</c> — so all of them are disabled. That is the normal state
/// for these handlers, not an edge case.
/// </para>
///
/// <para>
/// Reading one handler gives no hint of this, which is exactly why the same defect appeared twice
/// independently: <c>CreateClientFromLeadOnNegociacionHandler</c> and
/// <c>CreateClientFromLeadOnQuoteAcceptedHandler</c> both looked a client up by email alone and
/// could adopt another dealership's record. A convention cannot prevent that. This is the
/// mechanism.
/// </para>
///
/// <para>
/// The rule: inside a notification handler, a query on a tenant-scoped set must either match on an
/// id — GUIDs are globally unique, so an id lookup cannot cross tenants — or compare
/// <c>DealerId</c> explicitly. A predicate that does neither is matching on a natural key such as
/// an email, and will reach into every other dealership.
/// </para>
/// </summary>
public class OutboxHandlerTenantScopeTests
{
    /// <summary>DbSets carrying a global tenant filter, hence unprotected without one.</summary>
    private static readonly string[] TenantScopedSets =
    [
        "Cars", "Clients", "Quotes", "Sales", "Leads", "LeadActivities", "Appointments",
        "Transactions", "Documents", "Users", "DealerSettings", "ReconditioningTasks",
    ];

    private static readonly string[] QueryMethods =
    [
        "FirstOrDefaultAsync", "SingleOrDefaultAsync", "FirstAsync", "SingleAsync",
        "AnyAsync", "CountAsync", "ToListAsync", "Where", "Any", "Count",
    ];

    private static string SolutionRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "CleanArchitecture.sln")))
        {
            dir = dir.Parent;
        }

        dir.Should().NotBeNull("solution root must be found");
        return dir!.FullName;
    }

    [Fact]
    public void NotificationHandlers_Should_ScopeEveryNonIdLookupByDealer()
    {
        string applicationRoot = Path.Combine(SolutionRoot(), "src", "Application");
        var violations = new List<string>();

        foreach (string file in Directory.EnumerateFiles(applicationRoot, "*.cs", SearchOption.AllDirectories))
        {
            string source = File.ReadAllText(file);

            // Only the handlers that run off the outbox.
            if (!source.Contains("INotificationHandler", StringComparison.Ordinal))
            {
                continue;
            }

            CompilationUnitSyntax root = CSharpSyntaxTree.ParseText(source, path: file)
                .GetCompilationUnitRoot();

            foreach (InvocationExpressionSyntax invocation in
                     root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (invocation.Expression is not MemberAccessExpressionSyntax member ||
                    !QueryMethods.Contains(member.Name.Identifier.Text, StringComparer.Ordinal))
                {
                    continue;
                }

                string receiver = member.Expression.ToString();
                if (!TenantScopedSets.Any(set =>
                        receiver.Contains($"context.{set}", StringComparison.Ordinal)))
                {
                    continue;
                }

                string? predicate = invocation.ArgumentList.Arguments
                    .Select(a => a.Expression)
                    .OfType<LambdaExpressionSyntax>()
                    .Select(l => l.Body.ToString())
                    .FirstOrDefault();

                if (predicate is null)
                {
                    continue; // e.g. a bare ToListAsync(ct) — the Where before it is checked on its own.
                }

                // Safe when it matches on an id (globally unique) or scopes by dealer explicitly.
                bool matchesOnId = predicate.Contains("Id ==", StringComparison.Ordinal)
                                   || predicate.Contains("Id.Value ==", StringComparison.Ordinal)
                                   || predicate.Contains("Contains(", StringComparison.Ordinal);
                bool scopedByDealer = predicate.Contains("DealerId", StringComparison.Ordinal);

                if (!matchesOnId && !scopedByDealer)
                {
                    violations.Add(
                        $"{Path.GetFileName(file)}: {member.Name.Identifier.Text}({predicate})");
                }
            }
        }

        violations.Should().BeEmpty(
            "a notification handler runs with no tenant and every global filter disabled, so a " +
            "lookup that matches neither an id nor DealerId reaches into other dealerships. Add " +
            "`&& x.DealerId == <aggregate>.DealerId` to the predicate.");
    }
}

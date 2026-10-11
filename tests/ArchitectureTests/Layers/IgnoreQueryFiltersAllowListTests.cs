using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace ArchitectureTests.Layers;

/// <summary>
/// D2: <c>IgnoreQueryFilters()</c> switches off tenant isolation for a query. Every use inside
/// <c>src/Application</c> must live in a file of this explicit allow-list (or go through
/// <c>TenantQueryExtensions</c>). Adding a file to the list forces a change to this test, which
/// puts the decision in the review diff.
/// </summary>
public class IgnoreQueryFiltersAllowListTests
{
    /// <summary>Path prefixes (relative to <c>src/Application</c>, forward slashes) allowed to bypass filters.</summary>
    private static readonly string[] AllowedPrefixes =
    [
        // The tenant-aware wrapper itself.
        "Common/TenantQueryExtensions.cs",
        // Platform staff operate across dealerships by design.
        "Platform/",
        // Anonymous / pre-tenant flows: there is no tenant to filter by yet.
        "Users/Login/",
        "Users/Register/",
        "Users/ResetPassword/",
        "Users/ChangePassword/",
        // Outbox handlers run with no tenant context (HasTenant == false).
        "Sales/Events/",
        "Leads/CloseArtifacts/",
        // RN-013 (A1.1): retention handler reacts to billing events from the outbox (no tenant context);
        // its single query filters by the event DealerId explicitly.
        "Tenancy/StartTenantRetentionHandler.cs",
        // RN-014 (A1.3): the export builder and the expiry sweep run from Quartz with no tenant;
        // every query constrains DealerId explicitly (or sweeps platform-wide by design).
        "Tenancy/Export/",
        "Tenancy/Purge/",
        // Reviewed in QA remediation: every query below constrains DealerId explicitly
        // (or joins onto an already tenant-scoped set) after bypassing the soft-delete filter.
        "Appointments/Queries/GetAppointments/",
        "Clients/Restore/",
        "Clients/GetDeleted/",
        "Users/Commands/CreateUser/",
        "DealerSettings/Queries/GetHostName/",
        "DealerSettings/Commands/UpdateHostName/",
        "Leads/GetAll/",
        "Leads/GetById/",
        // spr ADR B9: the reconciliation job runs across ALL dealers with no tenant context by
        // design (it looks for dealers WITHOUT a subscription row); it only reads and flags.
        "Billing/Commands/ReconcileSubscriptions/",
        // Global catalogs without a tenant (Marca/Modelo) and the pre-tenant email lookup
        // used by the anonymous password-reset request.
        "Marcas/Create/",
        "Modelos/Create/",
        "Users/GetByEmail/",
    ];

    private static string ApplicationRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "CleanArchitecture.sln")))
        {
            dir = dir.Parent;
        }

        dir.Should().NotBeNull("solution root must be found");
        return Path.Combine(dir!.FullName, "src", "Application");
    }

    [Fact]
    public void IgnoreQueryFilters_Should_OnlyAppearInAllowListedFiles()
    {
        string root = ApplicationRoot();
        var violations = new List<string>();

        foreach (string file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            if (AllowedPrefixes.Any(p => relative.StartsWith(p, StringComparison.Ordinal)))
            {
                continue;
            }

            var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(file), path: file);
            foreach (InvocationExpressionSyntax call in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (call.Expression is MemberAccessExpressionSyntax m && m.Name.Identifier.Text == "IgnoreQueryFilters")
                {
                    int line = call.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                    violations.Add($"{relative}:{line}");
                }
            }
        }

        string.Join(Environment.NewLine, violations).Should().BeEmpty(
            "IgnoreQueryFilters() disables tenant isolation; use IgnoreQueryFiltersForTenant or add the file to the allow-list in this test.");
    }
}

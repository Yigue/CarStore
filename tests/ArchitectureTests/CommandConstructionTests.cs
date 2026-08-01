using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace ArchitectureTests;

/// <summary>
/// qa-p1-integridad PR5, Slice 10 (D6, REQ: crm-client-data-contract "Command Construction From
/// Endpoints Uses Named Arguments"). A convention ("use named arguments") is not a mechanism —
/// <c>Subscribe.cs</c> proved that by silently misaligning <c>CreateClientCommand</c>'s
/// positional string arguments for as long as the endpoint existed. This is the mechanism: a
/// Roslyn source-inspection test that fails any new <c>ObjectCreationExpression</c> under
/// <c>Web.Api/Endpoints</c> whose target type name ends <c>Command</c>/<c>Query</c> and passes
/// more than two <b>same-typed</b> positional (unnamed) arguments — the exact defect class that
/// makes a reordering mistake compile silently instead of failing loudly. Reflection resolves
/// each positional argument's declared parameter type from the real command/query record, rather
/// than trusting call-site syntax, so a swap between two differently-typed parameters (which the
/// compiler already catches) is not falsely flagged.
///
/// SPEC vs DESIGN correction (recorded, not silently absorbed): design.md D6 and tasks.md 10.3
/// describe the rule as "≥4 positional arguments" of any type. The binding acceptance criterion —
/// specs/crm-client-data-contract/spec.md, "Command Construction From Endpoints Uses Named
/// Arguments" — states it precisely as "more than two same-typed positional parameters". This
/// test implements the spec's wording (it is the acceptance criterion), not design.md's looser
/// paraphrase, and reflection-resolves each call site's real parameter types so a swap between
/// two DIFFERENTLY-typed parameters — which the C# compiler already rejects at build time — is
/// not falsely flagged (e.g. <c>CreateAppointmentCommand</c>'s 8 positional arguments span 5
/// distinct types, none repeating more than twice, so it is correctly NOT a violation).
///
/// ALLOWLIST SIZE correction (recorded, not silently absorbed): design.md D6 and tasks.md 10.4
/// claim exactly four pre-existing violations, "verified": Clients/Create.cs, Clients/Update.cs,
/// Leads/Create.cs, Cars/Create.cs. Running the spec's actual "same-typed" rule against the real
/// tree found FIFTEEN more pre-existing same-typed-positional sites design.md's audit missed
/// (Cars/Update.cs, Clients/Export.cs, Clients/Get.cs, DealerSettings/Update.cs,
/// DealerSettings/UpdateVisual.cs, Documents/DocumentsEndpoints.cs, Financial/Create.cs,
/// Financial/Update.cs, Quotes/CreateInquiry.cs, Sales/Create.cs, Users/Register.cs,
/// Users/UpdateMyProfile.cs, Users/CreateUser.cs, Users/UpdateUser.cs, Dealers/Provision.cs — 19
/// total). Spot-checked the riskiest ones (Financial/Create.cs, Financial/Update.cs,
/// Sales/Create.cs — all pass 3 same-typed <c>Nullable&lt;Guid&gt;</c>/<c>decimal</c> args
/// positionally) against their target command's declared parameter order: none reproduce
/// Subscribe.cs's swap defect, all map 1:1 in the correct order. Per design.md's own D6 decision
/// table ("Rule + a frozen, explicitly-shrinking allowlist of TODAY'S sites" — chosen; "Rule +
/// convert all violating call sites now" — deferred to Change 3), the ratchet's job is to freeze
/// existing debt and block new debt, not retroactively fix 15 unrelated endpoints inside a
/// two-CRITICAL PR. All 19 real pre-existing sites are grandfathered below so this test is
/// GREEN against the actual tree instead of failing on code this PR never touched.
///
/// ALLOWLIST EXPIRY (open question, design.md D6, now sized at 19 not 4): either a follow-up
/// change (Change 3) converts these call sites to named arguments and this allowlist shrinks
/// toward empty, or the ratchet is explicitly accepted as permanent at its current size. Do not
/// add new entries — from this commit forward, zero new same-typed wide-positional constructions
/// are permitted anywhere under Web.Api/Endpoints.
/// </summary>
public class CommandConstructionTests : BaseTest
{
    private const int SameTypedThreshold = 2;

    private static readonly HashSet<(string File, int Line)> Allowlist = new()
    {
        (Path.Combine("Cars", "Create.cs"), 39),
        (Path.Combine("Cars", "Update.cs"), 38),
        (Path.Combine("Clients", "Create.cs"), 30),
        (Path.Combine("Clients", "Export.cs"), 45),
        (Path.Combine("Clients", "Get.cs"), 31),
        (Path.Combine("Clients", "Update.cs"), 16),
        (Path.Combine("DealerSettings", "Update.cs"), 32),
        (Path.Combine("DealerSettings", "UpdateVisual.cs"), 23),
        (Path.Combine("Documents", "DocumentsEndpoints.cs"), 39),
        (Path.Combine("Financial", "Create.cs"), 29),
        (Path.Combine("Financial", "Update.cs"), 31),
        (Path.Combine("Leads", "Create.cs"), 23),
        (Path.Combine("Quotes", "CreateInquiry.cs"), 27),
        (Path.Combine("Sales", "Create.cs"), 31),
        (Path.Combine("Users", "Register.cs"), 22),
        (Path.Combine("Users", "UpdateMyProfile.cs"), 20),
        (Path.Combine("Users", "CreateUser.cs"), 32),
        (Path.Combine("Users", "UpdateUser.cs"), 32),
        (Path.Combine("Dealers", "Provision.cs"), 32),
    };

    private static readonly Assembly[] SearchAssemblies =
    {
        ApplicationAssembly,
        DomainAssembly,
        InfrastructureAssembly,
    };

    [Fact]
    public void Endpoints_DoNotConstructCommandsOrQueries_WithMoreThanTwoSameTypedPositionalArguments()
    {
        var endpointsRoot = GetEndpointsRoot();
        var violations = new List<string>();
        var unresolvedTypes = new List<string>();

        foreach (var file in Directory.EnumerateFiles(endpointsRoot, "*.cs", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(endpointsRoot, file);
            var source = File.ReadAllText(file);
            var tree = CSharpSyntaxTree.ParseText(source, path: file);
            var root = tree.GetCompilationUnitRoot();

            foreach (var creation in root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
            {
                var typeName = creation.Type switch
                {
                    IdentifierNameSyntax identifier => identifier.Identifier.Text,
                    GenericNameSyntax generic => generic.Identifier.Text,
                    QualifiedNameSyntax qualified => qualified.Right.Identifier.Text,
                    _ => creation.Type.ToString(),
                };

                if (!typeName.EndsWith("Command", StringComparison.Ordinal)
                    && !typeName.EndsWith("Query", StringComparison.Ordinal))
                {
                    continue;
                }

                var arguments = creation.ArgumentList?.Arguments;
                if (arguments is null || arguments.Value.Count == 0)
                {
                    continue;
                }

                // Positional arguments are always the leading contiguous run — C# requires every
                // named argument to follow every positional one in a single call.
                var positionalCount = arguments.Value.TakeWhile(a => a.NameColon is null).Count();
                if (positionalCount < SameTypedThreshold + 1)
                {
                    continue;
                }

                var targetType = ResolveType(typeName);
                if (targetType is null)
                {
                    unresolvedTypes.Add($"{relativePath} — {typeName} (could not resolve via reflection)");
                    continue;
                }

                var ctorParameters = targetType.GetConstructors()
                    .OrderByDescending(c => c.GetParameters().Length)
                    .First()
                    .GetParameters();

                var lineNumber = creation.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

                if (Allowlist.Contains((relativePath, lineNumber)))
                {
                    continue;
                }

                var sameTypedGroupCounts = ctorParameters
                    .Take(Math.Min(positionalCount, ctorParameters.Length))
                    .GroupBy(p => p.ParameterType)
                    .Select(g => (Type: g.Key, Count: g.Count()))
                    .Where(g => g.Count > SameTypedThreshold)
                    .ToList();

                if (sameTypedGroupCounts.Count == 0)
                {
                    continue;
                }

                var description = string.Join(", ",
                    sameTypedGroupCounts.Select(g => $"{g.Count}x {g.Type.Name}"));

                violations.Add($"{relativePath}:{lineNumber} — {typeName} passes {description} positionally");
            }
        }

        unresolvedTypes.Should().BeEmpty(
            "every *Command/*Query construction under Web.Api/Endpoints must resolve to a real type " +
            "via reflection so this test can inspect its declared parameter types");

        violations.Should().BeEmpty(
            "new Command/Query call sites under Web.Api/Endpoints with more than two same-typed " +
            "positional arguments must use named arguments (qa-p1-integridad D6, " +
            "crm-client-data-contract spec) unless explicitly grandfathered in the allowlist");
    }

    private static Type? ResolveType(string typeName)
    {
        foreach (var assembly in SearchAssemblies)
        {
            var match = assembly.GetTypes().FirstOrDefault(t => t.Name == typeName);
            if (match is not null)
            {
                return match;
            }
        }

        return null;
    }

    private static string GetEndpointsRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "CleanArchitecture.sln")))
        {
            dir = dir.Parent;
        }

        if (dir is null)
        {
            throw new InvalidOperationException(
                "Could not locate solution root (CleanArchitecture.sln) walking up from " + AppContext.BaseDirectory);
        }

        var endpointsRoot = Path.Combine(dir.FullName, "src", "Web.Api", "Endpoints");
        Directory.Exists(endpointsRoot).Should().BeTrue($"expected {endpointsRoot} to exist");
        return endpointsRoot;
    }
}

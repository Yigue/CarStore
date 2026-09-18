using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Domain.Users;
using FluentAssertions;
using Xunit;

namespace ArchitectureTests;

/// <summary>
/// CFG-03. The permissions screen and the rules the API enforces used to be two unrelated lists:
/// endpoints guarded themselves with <c>cars:read</c> and <c>quotes:accept</c>, while the screen
/// offered a hardcoded set of invented names no endpoint has ever checked. A dealership could tick
/// every box and still have a user who could not open the inventory.
///
/// <para>
/// <see cref="PermissionCatalog"/> is now the single source of truth, and these tests are what
/// keeps it one: a new endpoint guarded by a permission the catalogue omits fails the build, here,
/// instead of shipping an access nobody can grant.
/// </para>
/// </summary>
public class PermissionCatalogTests
{
    /// <summary>
    /// Matches both shapes the endpoints use: a literal (<c>.HasPermission("clients:read")</c>)
    /// and the per-module constant (<c>.HasPermission(Permissions.CarsRead)</c>).
    /// </summary>
    private static readonly Regex HasPermissionCall =
        new(@"\.HasPermission\(\s*(?<arg>""(?<literal>[^""]+)""|Permissions\.(?<constant>[A-Za-z0-9_]+))\s*\)",
            RegexOptions.Compiled);

    private static readonly Regex ConstantDeclaration =
        new(@"internal\s+const\s+string\s+(?<name>[A-Za-z0-9_]+)\s*=\s*""(?<value>[^""]+)""",
            RegexOptions.Compiled);

    /// <summary>
    /// Platform permissions belong to CarStore's own staff operating the SaaS, not to a
    /// dealership configuring its roles — offering them in the matrix would invite an
    /// administrator to grant an access their tenancy cannot contain.
    /// </summary>
    private const string PlatformPrefix = "platform:";

    [Fact]
    public void EveryPermissionGuardingAnEndpoint_IsGrantableFromTheCatalogue()
    {
        var endpointsRoot = GetEndpointsRoot();
        var constants = LoadPermissionConstants(endpointsRoot);
        var missing = new List<string>();

        foreach (var file in Directory.EnumerateFiles(endpointsRoot, "*.cs", SearchOption.AllDirectories))
        {
            string relativePath = Path.GetRelativePath(endpointsRoot, file);

            foreach (Match match in HasPermissionCall.Matches(File.ReadAllText(file)))
            {
                string? value = match.Groups["literal"].Success
                    ? match.Groups["literal"].Value
                    : ResolveConstant(constants, relativePath, match.Groups["constant"].Value);

                if (value is null || value.StartsWith(PlatformPrefix, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!PermissionCatalog.IsGrantable(value))
                {
                    missing.Add($"{relativePath} — '{value}'");
                }
            }
        }

        missing.Distinct().Should().BeEmpty(
            "every permission an endpoint guards itself with has to be one a dealership can grant " +
            "from the roles screen (PermissionCatalog). A permission the API demands and the " +
            "catalogue omits is an access nobody can hand out.");
    }

    [Fact]
    public void TheCatalogue_HasNoDuplicateValues()
    {
        PermissionCatalog.All
            .GroupBy(p => p.Value, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .Should().BeEmpty("a permission listed twice renders twice in the matrix");
    }

    [Fact]
    public void EveryPermission_CarriesAModuleAndALabel()
    {
        PermissionCatalog.All
            .Where(p => string.IsNullOrWhiteSpace(p.Module) || string.IsNullOrWhiteSpace(p.Label))
            .Select(p => p.Value)
            .Should().BeEmpty("the matrix groups by module and reads the label — an unlabelled row is a checkbox nobody can interpret");
    }

    // The catalogue is case-sensitive because the endpoints compare the claim exactly: a
    // near-miss would be stored, shown as granted, and never match.
    [Fact]
    public void Grantability_IsCaseSensitive()
    {
        PermissionCatalog.IsGrantable("cars:read").Should().BeTrue();
        PermissionCatalog.IsGrantable("Cars:Read").Should().BeFalse();
    }

    [Fact]
    public void PlatformPermissions_AreNotOfferedToDealerships()
    {
        PermissionCatalog.All
            .Where(p => p.Value.StartsWith(PlatformPrefix, StringComparison.Ordinal))
            .Should().BeEmpty("platform:* belongs to the SaaS operator, not to a dealership's roles");
    }

    private static string? ResolveConstant(
        IReadOnlyDictionary<string, Dictionary<string, string>> constants,
        string relativePath,
        string constantName)
    {
        // Permissions.cs is per-module and `Permissions.X` resolves to the one in the endpoint's
        // own folder, so look there first; fall back to any module that declares the name for the
        // handful of endpoints that import a sibling's class.
        string? folder = Path.GetDirectoryName(relativePath);

        if (folder is not null
            && constants.TryGetValue(folder, out var own)
            && own.TryGetValue(constantName, out string? ownValue))
        {
            return ownValue;
        }

        foreach (var module in constants.Values)
        {
            if (module.TryGetValue(constantName, out string? value))
            {
                return value;
            }
        }

        return null;
    }

    private static Dictionary<string, Dictionary<string, string>> LoadPermissionConstants(string endpointsRoot)
    {
        var byFolder = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);

        foreach (var file in Directory.EnumerateFiles(endpointsRoot, "Permissions.cs", SearchOption.AllDirectories))
        {
            string folder = Path.GetDirectoryName(Path.GetRelativePath(endpointsRoot, file)) ?? string.Empty;
            var declared = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (Match match in ConstantDeclaration.Matches(File.ReadAllText(file)))
            {
                declared[match.Groups["name"].Value] = match.Groups["value"].Value;
            }

            byFolder[folder] = declared;
        }

        return byFolder;
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

        string endpointsRoot = Path.Combine(dir.FullName, "src", "Web.Api", "Endpoints");
        Directory.Exists(endpointsRoot).Should().BeTrue($"expected {endpointsRoot} to exist");
        return endpointsRoot;
    }
}

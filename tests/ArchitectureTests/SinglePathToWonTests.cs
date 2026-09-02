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
/// REQ-2.1: a lead reaches <c>Ganado</c> only where a sale exists. One path, not four.
///
/// <para>
/// Four places used to write the stage. Two of them could not know whether a sale existed:
/// <c>UpdateLeadStatusFromQuoteHandler</c> fired on quote acceptance, and
/// <c>ConvertLeadToClientCommandHandler</c> fired on manual conversion. Both closed the deal in
/// the pipeline while the sale record that gives <c>Ganado</c> its meaning did not exist, so the
/// won-deal count and the revenue figures disagreed by construction.
/// </para>
///
/// <para>
/// A unit test cannot hold this line. Each writer looks reasonable read on its own, which is how
/// four of them accumulated; and a test that drives one handler proves nothing about the next one
/// somebody adds. So the rule is enforced over the source: inside <c>src/Application</c>, only
/// the two members below may pass <c>LeadStatus.Ganado</c> to a stage-changing call. Both earn it
/// — one observes <c>SaleCompletedDomainEvent</c>, the other refuses the command outright unless
/// <c>HasSaleAsync</c> returns true.
/// </para>
///
/// <para>
/// Adding a third writer is not forbidden; it is forbidden <i>silently</i>. Whoever adds one has
/// to come here, name it, and say which fact makes it a sale.
/// </para>
/// </summary>
public class SinglePathToWonTests
{
    /// <summary>The only files allowed to move a lead to Ganado, and the fact each one observes.</summary>
    private static readonly Dictionary<string, string> AuthorisedWriters = new(StringComparer.Ordinal)
    {
        ["UpdateLeadStatusFromSaleHandler.cs"] = "observes SaleCompletedDomainEvent — the sale is the trigger",
        ["UpdateLeadStatusCommandHandler.cs"] = "refuses the command unless HasSaleAsync finds one",
        ["CreateSaleCommandHandler.cs"] = "creates the sale",
    };

    /// <summary>Methods that write the stage. Reading it — a comparison — is always fine.</summary>
    private static readonly string[] StageWriters = ["ForceStatus", "ChangeStatus", "UpdateStatus"];

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
    public void OnlyASale_Should_MoveALeadToGanado()
    {
        string applicationRoot = Path.Combine(SolutionRoot(), "src", "Application");
        var violations = new List<string>();

        foreach (string file in Directory.EnumerateFiles(applicationRoot, "*.cs", SearchOption.AllDirectories))
        {
            string fileName = Path.GetFileName(file);
            if (AuthorisedWriters.ContainsKey(fileName))
            {
                continue;
            }

            string source = File.ReadAllText(file);
            if (!source.Contains("LeadStatus.Ganado", StringComparison.Ordinal))
            {
                continue;
            }

            CompilationUnitSyntax root = CSharpSyntaxTree.ParseText(source, path: file)
                .GetCompilationUnitRoot();

            foreach (InvocationExpressionSyntax invocation in
                     root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (invocation.Expression is not MemberAccessExpressionSyntax member ||
                    !StageWriters.Contains(member.Name.Identifier.Text, StringComparer.Ordinal))
                {
                    continue;
                }

                bool writesGanado = invocation.ArgumentList.Arguments
                    .Any(argument => argument.ToString().Contains("LeadStatus.Ganado", StringComparison.Ordinal));

                if (writesGanado)
                {
                    violations.Add(
                        $"{fileName}: {member.Name.Identifier.Text}(LeadStatus.Ganado) — this file cannot " +
                        "know whether a sale exists.");
                }
            }
        }

        violations.Should().BeEmpty(
            "Ganado must follow a sale. Authorised writers: {0}. To add another, register it in " +
            "AuthorisedWriters with the fact it observes.",
            string.Join(", ", AuthorisedWriters.Keys));
    }
}

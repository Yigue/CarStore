using FluentAssertions;
using System.Text.RegularExpressions;

namespace ArchitectureTests;

/// <summary>
/// ADR C1 / B0.1.3 ratchet: Domain must state the currency of every amount it creates
/// (<c>Money.Of(x, CurrencyCode.…)</c> or the two-argument constructor). The legacy one-argument
/// <c>new Money(x)</c> silently means ARS and is what made USD amounts degrade to ARS.
/// Money.cs itself is exempt (it defines the legacy default).
/// </summary>
public class MoneyCurrencyRatchetTests
{
    // `new Money(` followed by arguments containing no top-level comma.
    private static readonly Regex SingleArgNewMoney =
        new(@"new\s+Money\(((?:[^,()]|\([^()]*\))*)\)", RegexOptions.Compiled);

    [Fact]
    public void Domain_Should_NotUseTheOneArgumentMoneyConstructor()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "CleanArchitecture.sln")))
            dir = dir.Parent;
        dir.Should().NotBeNull("the solution root must be reachable from the test output directory");

        string domainRoot = Path.Combine(dir!.FullName, "src", "Domain");
        var offenders = Directory.EnumerateFiles(domainRoot, "*.cs", SearchOption.AllDirectories)
            .Where(f => Path.GetFileName(f) != "Money.cs")
            .SelectMany(f => File.ReadLines(f)
                .Select((line, i) => (File: Path.GetRelativePath(domainRoot, f), Line: i + 1, Text: line)))
            .Where(x => !x.Text.TrimStart().StartsWith("//") && SingleArgNewMoney.IsMatch(x.Text))
            .Select(x => $"{x.File}:{x.Line}")
            .ToList();

        offenders.Should().BeEmpty("Domain must use Money.Of(amount, CurrencyCode) and never rely on the implicit default currency");
    }
}

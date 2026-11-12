using System;
using System.IO;
using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace WebApiTests.Legal;

/// <summary>
/// A0.2.2: the published Terms text (<c>content/legal/terminos/{version}.md</c>, repo root) must hash to
/// <c>Legal:Terms:Sha256</c>, otherwise the acceptance records (RN-016) would point at a different text.
/// </summary>
public class LegalContentHashTests
{
    private static IConfigurationSection Terms()
    {
        string appsettings = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        IConfigurationRoot config = new ConfigurationBuilder().AddJsonFile(appsettings, optional: false).Build();
        return config.GetSection("Legal:Terms");
    }

    private static string? FindLegalContentRoot()
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            string candidate = Path.Combine(dir.FullName, "content", "legal");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    [Fact]
    public void TermsMarkdown_Sha256_MatchesConfiguredHash()
    {
        string? root = FindLegalContentRoot();
        Assert.True(root is not null, "content/legal was not found walking up from the test output directory.");

        IConfigurationSection terms = Terms();
        string version = terms["Version"]!;
        string path = Path.Combine(root!, "terminos", $"{version}.md");
        Assert.True(File.Exists(path), $"Missing published terms file: {path}");

        string actual = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

        Assert.Equal(terms["Sha256"]!.ToLowerInvariant(), actual);
    }

    [Fact]
    public void PrivacyMarkdown_ForConfiguredVersion_Exists()
    {
        string? root = FindLegalContentRoot();
        Assert.True(root is not null, "content/legal was not found walking up from the test output directory.");

        string version = Terms()["Version"]!;
        Assert.True(File.Exists(Path.Combine(root!, "privacidad", $"{version}.md")));
    }
}

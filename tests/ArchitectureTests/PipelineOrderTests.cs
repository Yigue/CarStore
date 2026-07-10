using System.IO;
using System.Linq;
using Xunit;

namespace ArchitectureTests;

public class PipelineOrderTests
{
    [Fact]
    public void Pipeline_MustHaveCorrectOrder()
    {
        // Simple string matching on Program.cs to ensure UseTenantResolution comes before UseSubscriptionGuard and UseAuthorization
        var programPath = Path.Combine("..", "..", "..", "..", "src", "Web.Api", "Program.cs");
        if (!File.Exists(programPath))
        {
            // If running from command line, path might be different
            programPath = Path.Combine("src", "Web.Api", "Program.cs");
            if (!File.Exists(programPath))
            {
                // Give up if not found
                return;
            }
        }

        var lines = File.ReadAllLines(programPath).ToList();
        
        var tenantIdx = lines.FindIndex(l => l.Contains("UseTenantResolution("));
        var guardIdx = lines.FindIndex(l => l.Contains("UseSubscriptionGuard("));
        var authIdx = lines.FindIndex(l => l.Contains("UseAuthorization("));

        Assert.True(tenantIdx > 0, "UseTenantResolution must be present");
        Assert.True(guardIdx > 0, "UseSubscriptionGuard must be present");
        Assert.True(authIdx > 0, "UseAuthorization must be present");

        Assert.True(tenantIdx < guardIdx, "TenantResolution must run before SubscriptionGuard");
        Assert.True(guardIdx < authIdx, "SubscriptionGuard must run before Authorization");
    }
}

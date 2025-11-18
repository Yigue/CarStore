using System.Collections.Generic;
using System.Threading.Tasks;
using Infrastructure.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfrastructureTests.Storage;

public class AzureBlobStorageServiceTests
{
    [Fact]
    public void Constructor_Throws_WhenConnectionStringMissing()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();
        Action act = () => new AzureBlobStorageService(configuration, NullLogger<AzureBlobStorageService>.Instance);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task GenerateSasUri_ReturnsUri()
    {
        string accountKey = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("dummyaccountkeydummyaccountkey"));
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AzureBlobStorage:ConnectionString"] = $"DefaultEndpointsProtocol=https;AccountName=testaccount;AccountKey={accountKey};EndpointSuffix=core.windows.net"
        }).Build();

        var service = new AzureBlobStorageService(configuration, NullLogger<AzureBlobStorageService>.Instance);

        var uri = await service.GenerateAccessUrlAsync("container", "blob");

        uri.Should().NotBeNull();
        uri.Should().Contain("sig=");
    }
}

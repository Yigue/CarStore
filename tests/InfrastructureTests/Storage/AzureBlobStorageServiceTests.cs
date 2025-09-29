using System.Collections.Generic;
using Azure.Storage.Blobs;
using Infrastructure.Storage;
using Microsoft.Extensions.Configuration;

namespace InfrastructureTests.Storage;

public class AzureBlobStorageServiceTests
{
    [Fact]
    public void Constructor_Throws_WhenConnectionStringMissing()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();
        Action act = () => new AzureBlobStorageService(configuration);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GenerateSasUri_ReturnsUri()
    {
        string accountKey = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("dummyaccountkeydummyaccountkey"));
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AzureBlobStorage:ConnectionString"] = $"DefaultEndpointsProtocol=https;AccountName=testaccount;AccountKey={accountKey};EndpointSuffix=core.windows.net"
        }).Build();

        var service = new AzureBlobStorageService(configuration);

        var blobClient = new BlobClient(new Uri("https://testaccount.blob.core.windows.net/container/blob"));

        var uri = service.GenerateSasUri(blobClient);

        uri.Should().NotBeNull();
        uri.Query.Should().Contain("sig=");
    }
}

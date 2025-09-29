using System.Collections.Generic;
using System.Text;
using Infrastructure.Storage;
using Microsoft.Extensions.Configuration;

namespace InfrastructureTests.Storage;

public class LocalFileStorageServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly LocalFileStorageService _service;

    public LocalFileStorageServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["LocalStorage:BasePath"] = _tempDir,
            ["LocalStorage:BaseUrl"] = "/files"
        }).Build();
        _service = new LocalFileStorageService(configuration);
    }

    [Fact]
    public async Task UploadDownloadAndDelete_WorksCorrectly()
    {
        var container = "cars";
        var blob = "test.txt";
        var data = Encoding.UTF8.GetBytes("hello world");

        var url = await _service.UploadAsync(container, blob, data);
        url.Should().Be($"/files/{container}/{blob}");

        (await _service.ExistsAsync(container, blob)).Should().BeTrue();

        var downloaded = await _service.DownloadAsync(container, blob);
        downloaded.Should().Equal(data);

        await _service.DeleteAsync(container, blob);
        (await _service.ExistsAsync(container, blob)).Should().BeFalse();

        Func<Task> act = async () => await _service.DownloadAsync(container, blob);
        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }
}

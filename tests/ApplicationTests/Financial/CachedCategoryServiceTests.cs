using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions.Caching;
using Application.Abstractions.Data;
using Infrastructure.Caching;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Application.UnitTests.Financial;

public class CachedCategoryServiceTests
{
    private readonly Mock<IApplicationDbContext> _contextMock = new();
    private readonly Mock<ICacheService> _cacheServiceMock = new();

    [Fact]
    public async Task InvalidateCacheAsync_ShouldCallRemoveAsync_WithAllCategoriesKey()
    {
        // Arrange
        var service = new CachedCategoryService(
            _contextMock.Object,
            _cacheServiceMock.Object,
            NullLogger<CachedCategoryService>.Instance);

        // Act
        await service.InvalidateCacheAsync(CancellationToken.None);

        // Assert
        _cacheServiceMock.Verify(
            c => c.RemoveAsync(
                CacheKeys.AllTransactionCategories(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}

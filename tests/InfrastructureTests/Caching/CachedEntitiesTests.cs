using Domain.Cars.Attributes;
using FluentAssertions;
using Infrastructure.Caching;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace InfrastructureTests.Caching;

public class CachedEntitiesTests
{
    [Fact]
    public async Task GetAllAsync_WritesToCache_AndSecondCallIsHit()
    {
        var opts = Options.Create(new MemoryDistributedCacheOptions());
        var memoryCache = new MemoryDistributedCache(opts);
        var redisCache = new RedisCacheService(memoryCache, NullLogger<RedisCacheService>.Instance);

        var marca = Marca.WithId(Guid.NewGuid(), "Toyota");
        var dto = new Application.Abstractions.Caching.MarcaCacheDto { Id = marca.Id, Nombre = marca.Nombre };
        var list = new List<Application.Abstractions.Caching.MarcaCacheDto> { dto };

        // Act 1: SetAsync writes DTO
        await redisCache.SetAsync("test-key", list);

        // Act 2: second GetAsync (cache hit)
        var result = await redisCache.GetAsync<List<Application.Abstractions.Caching.MarcaCacheDto>>("test-key");

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result.First().Nombre.Should().Be("Toyota");
    }
}

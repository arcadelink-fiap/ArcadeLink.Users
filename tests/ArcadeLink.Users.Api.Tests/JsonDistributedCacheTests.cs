using ArcadeLink.Users.Api.Caching;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Xunit;

namespace ArcadeLink.Users.Api.Tests;

public class JsonDistributedCacheTests
{
    private static IDistributedCache NewCache() =>
        new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

    private record Sample(string Value);

    [Fact]
    public async Task Get_returns_default_on_miss()
    {
        var cache = NewCache();
        var result = await JsonDistributedCache.GetAsync<Sample>(cache, "missing-key");
        Assert.Null(result);
    }

    [Fact]
    public async Task Set_then_get_round_trips_the_value()
    {
        var cache = NewCache();
        await JsonDistributedCache.SetAsync(cache, "k", new Sample("x"), TimeSpan.FromSeconds(60));

        var result = await JsonDistributedCache.GetAsync<Sample>(cache, "k");

        Assert.Equal(new Sample("x"), result);
    }

    [Fact]
    public async Task Remove_makes_a_previously_cached_value_miss_again()
    {
        var cache = NewCache();
        await JsonDistributedCache.SetAsync(cache, "k", new Sample("x"), TimeSpan.FromSeconds(60));

        await cache.RemoveAsync("k");

        var result = await JsonDistributedCache.GetAsync<Sample>(cache, "k");
        Assert.Null(result);
    }
}

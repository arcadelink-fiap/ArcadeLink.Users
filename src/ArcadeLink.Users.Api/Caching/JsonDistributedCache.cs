using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;

namespace ArcadeLink.Users.Api.Caching;

public static class JsonDistributedCache
{
    public static async Task<T?> GetAsync<T>(IDistributedCache cache, string key)
    {
        var value = await cache.GetStringAsync(key);
        return value is null ? default : JsonSerializer.Deserialize<T>(value);
    }

    public static Task SetAsync<T>(IDistributedCache cache, string key, T value, TimeSpan ttl) =>
        cache.SetStringAsync(
            key,
            JsonSerializer.Serialize(value),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl });
}

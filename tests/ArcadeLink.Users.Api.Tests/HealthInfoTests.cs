using ArcadeLink.Users.Api.Health;
using Microsoft.Extensions.Configuration;

namespace ArcadeLink.Users.Api.Tests;

public class HealthInfoTests
{
    private static IConfiguration Config(params (string Key, string? Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value)))
            .Build();

    [Fact]
    public void Uses_APP_VERSION_when_present()
    {
        var result = HealthInfo.Create(Config(("APP_VERSION", "abc123")), "users-api");

        Assert.Equal("healthy", result.Status);
        Assert.Equal("users-api", result.Service);
        Assert.Equal("abc123", result.Version);
    }

    [Fact]
    public void Falls_back_to_dev()
    {
        Assert.Equal("dev", HealthInfo.Create(Config(), "users-api").Version);
        Assert.Equal("dev", HealthInfo.Create(Config(("APP_VERSION", "")), "users-api").Version);
    }
}

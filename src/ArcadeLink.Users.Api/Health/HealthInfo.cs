namespace ArcadeLink.Users.Api.Health;

public record HealthResponse(string Status, string Service, string Version);

public static class HealthInfo
{
    public static HealthResponse Create(IConfiguration configuration, string service)
    {
        var version = configuration["APP_VERSION"];
        return new HealthResponse("healthy", service, string.IsNullOrWhiteSpace(version) ? "dev" : version);
    }
}

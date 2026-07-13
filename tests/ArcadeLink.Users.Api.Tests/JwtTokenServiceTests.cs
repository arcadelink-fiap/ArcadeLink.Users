using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using ArcadeLink.Users.Api.Auth;
using ArcadeLink.Users.Api.Domain;
using Microsoft.Extensions.Options;
using Xunit;

namespace ArcadeLink.Users.Api.Tests;

public class JwtTokenServiceTests
{
    private static JwtTokenService CreateService() => new(Options.Create(new JwtOptions
    {
        Issuer = "ArcadeLink.Users",
        Audience = "ArcadeLink",
        Key = "test-only-signing-key-at-least-32-bytes-long",
        ExpiresInMinutes = 60
    }));

    [Fact]
    public void GenerateToken_includes_issuer_audience_and_role_claim()
    {
        var service = CreateService();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = "Ada Lovelace",
            Email = "ada@example.com",
            Role = UserRole.Admin
        };

        var (token, expiresAtUtc) = service.GenerateToken(user);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Equal("ArcadeLink.Users", jwt.Issuer);
        Assert.Contains("ArcadeLink", jwt.Audiences);
        Assert.Equal(user.Id.ToString(), jwt.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Sub).Value);
        Assert.Equal(nameof(UserRole.Admin), jwt.Claims.Single(c => c.Type == ClaimTypes.Role).Value);
        Assert.True(expiresAtUtc > DateTime.UtcNow);
    }
}

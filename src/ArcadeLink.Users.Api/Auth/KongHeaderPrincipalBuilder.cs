using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace ArcadeLink.Users.Api.Auth;

public static class KongHeaderPrincipalBuilder
{
    public const string SchemeName = "KongHeader";

    public static ClaimsPrincipal? TryBuild(IHeaderDictionary headers)
    {
        if (!headers.TryGetValue("X-User-Id", out var userIdValues) ||
            !Guid.TryParse(userIdValues.ToString(), out var userId))
        {
            return null;
        }

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(ClaimTypes.NameIdentifier, userId.ToString())
        };

        if (headers.TryGetValue("X-User-Role", out var roleValues) && !string.IsNullOrWhiteSpace(roleValues.ToString()))
            claims.Add(new Claim(ClaimTypes.Role, roleValues.ToString()!));

        var identity = new ClaimsIdentity(claims, SchemeName);
        return new ClaimsPrincipal(identity);
    }
}

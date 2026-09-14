// FindFirstValue é um extension method de ClaimsPrincipal (System.Security.Claims.PrincipalExtensions,
// em Microsoft.Extensions.Identity.Core) — sem este using o teste não compila.
using System.Security.Claims;
using ArcadeLink.Users.Api.Auth;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace ArcadeLink.Users.Api.Tests;

public class KongHeaderPrincipalBuilderTests
{
    [Fact]
    public void Returns_null_when_user_id_header_missing()
    {
        var headers = new HeaderDictionary();

        var principal = KongHeaderPrincipalBuilder.TryBuild(headers);

        Assert.Null(principal);
    }

    [Fact]
    public void Returns_null_when_user_id_header_is_not_a_guid()
    {
        var headers = new HeaderDictionary { ["X-User-Id"] = "not-a-guid" };

        var principal = KongHeaderPrincipalBuilder.TryBuild(headers);

        Assert.Null(principal);
    }

    [Fact]
    public void Builds_principal_with_id_and_role_when_headers_present()
    {
        var userId = Guid.NewGuid();
        var headers = new HeaderDictionary
        {
            ["X-User-Id"] = userId.ToString(),
            ["X-User-Role"] = "Admin"
        };

        var principal = KongHeaderPrincipalBuilder.TryBuild(headers);

        Assert.NotNull(principal);
        Assert.True(principal!.IsInRole("Admin"));
        Assert.Equal(userId.ToString(), principal.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub));
    }

    [Fact]
    public void Builds_principal_without_role_claim_when_role_header_absent()
    {
        var userId = Guid.NewGuid();
        var headers = new HeaderDictionary { ["X-User-Id"] = userId.ToString() };

        var principal = KongHeaderPrincipalBuilder.TryBuild(headers);

        Assert.NotNull(principal);
        Assert.False(principal!.IsInRole("Admin"));
    }
}

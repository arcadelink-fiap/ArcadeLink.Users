using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ArcadeLink.Users.Api.Auth;

public class KongHeaderAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var principal = KongHeaderPrincipalBuilder.TryBuild(Request.Headers);
        if (principal is null)
            return Task.FromResult(AuthenticateResult.NoResult());

        var ticket = new AuthenticationTicket(principal, KongHeaderPrincipalBuilder.SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

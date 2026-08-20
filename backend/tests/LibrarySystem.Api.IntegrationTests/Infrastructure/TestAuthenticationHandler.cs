using System.Security.Claims;
using System.Text.Encodings.Web;
using LibrarySystem.Shared.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LibrarySystem.Api.IntegrationTests.Infrastructure;

internal sealed class TestAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string AuthenticationScheme = "Test";
    public const string HeaderName = "X-Test-User";
    public const string UserIdHeaderName = "X-Test-User-Id";
    public const string RolesHeaderName = "X-Test-Roles";
    public const string UserName = "integration-test-user";
    public const string UserId = "integration-test-user-id";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var userName) || string.IsNullOrWhiteSpace(userName))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var userId = Request.Headers.TryGetValue(UserIdHeaderName, out var requestedUserId) &&
            !string.IsNullOrWhiteSpace(requestedUserId)
                ? requestedUserId.ToString()
                : UserId;

        string[] roles = Request.Headers.TryGetValue(RolesHeaderName, out var requestedRoles) &&
            !string.IsNullOrWhiteSpace(requestedRoles)
                ? requestedRoles.ToString()
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                : [IdentityRoles.Admin];

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Name, userName!)
        };

        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var identity = new ClaimsIdentity(claims, AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, AuthenticationScheme);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

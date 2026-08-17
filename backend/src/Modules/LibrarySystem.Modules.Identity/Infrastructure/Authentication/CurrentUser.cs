using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using LibrarySystem.Shared.Authentication;
using Microsoft.AspNetCore.Http;

namespace LibrarySystem.Modules.Identity.Infrastructure.Authentication;

internal sealed class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public string? UserId
    {
        get
        {
            var user = httpContextAccessor.HttpContext?.User;

            return user?.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? user?.FindFirstValue(JwtRegisteredClaimNames.Sub);
        }
    }

    public bool IsAuthenticated =>
        httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated == true;
}

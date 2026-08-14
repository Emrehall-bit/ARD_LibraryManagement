using LibrarySystem.Modules.Identity.Application.Dtos;
using LibrarySystem.Modules.Identity.Domain;

namespace LibrarySystem.Modules.Identity.Application.Interfaces;

internal interface ITokenService
{
    AuthResponseDto CreateAccessToken(ApplicationUser user);
}

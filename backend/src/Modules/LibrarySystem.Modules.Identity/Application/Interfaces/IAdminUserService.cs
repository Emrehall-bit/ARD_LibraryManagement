using LibrarySystem.Modules.Identity.Application.Dtos;

namespace LibrarySystem.Modules.Identity.Application.Interfaces;

public interface IAdminUserService
{
    Task<PagedAdminUsersResponseDto> GetUsersAsync(
        GetAdminUsersQueryDto query,
        CancellationToken cancellationToken = default);
}

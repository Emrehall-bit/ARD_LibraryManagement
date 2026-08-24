using LibrarySystem.Modules.Identity.Application.Dtos;
using LibrarySystem.Modules.Identity.Application.Interfaces;
using LibrarySystem.Shared.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace LibrarySystem.Modules.Identity.Presentation;

[ApiController]
[Authorize(Roles = IdentityRoles.Admin)]
[Route("api/admin/users")]
public sealed class AdminUsersController(IAdminUserService adminUserService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedAdminUsersResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedAdminUsersResponseDto>> GetUsers(
        [FromQuery] GetAdminUsersQueryDto query,
        CancellationToken cancellationToken)
    {
        var users = await adminUserService.GetUsersAsync(query, cancellationToken);

        return Ok(users);
    }
}

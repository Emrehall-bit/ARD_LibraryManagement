namespace LibrarySystem.Modules.Identity.Application.Dtos;

public sealed record PagedAdminUsersResponseDto(
    IReadOnlyList<AdminUserResponseDto> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);

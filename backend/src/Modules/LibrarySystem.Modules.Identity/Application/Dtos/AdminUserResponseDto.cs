namespace LibrarySystem.Modules.Identity.Application.Dtos;

public sealed record AdminUserResponseDto(
    string Id,
    string Username,
    string? Email,
    IReadOnlyList<string> Roles,
    int ActiveBorrowCount,
    int OverdueBorrowCount);

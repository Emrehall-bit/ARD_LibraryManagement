namespace LibrarySystem.Modules.Books.Application.Dtos;

public sealed record BookImageResponseDto(
    Guid Id,
    string ObjectName,
    bool IsCover,
    int SortOrder);

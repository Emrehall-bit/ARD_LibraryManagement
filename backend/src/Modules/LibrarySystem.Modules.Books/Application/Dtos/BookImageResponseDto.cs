namespace LibrarySystem.Modules.Books.Application.Dtos;

public sealed record BookImageResponseDto(
    Guid Id,
    string Url,
    bool IsCover,
    int SortOrder);

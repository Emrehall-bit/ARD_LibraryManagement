namespace LibrarySystem.Modules.Books.Application.Dtos;

public sealed record PagedBooksResponseDto(
    IReadOnlyList<BookResponseDto> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);

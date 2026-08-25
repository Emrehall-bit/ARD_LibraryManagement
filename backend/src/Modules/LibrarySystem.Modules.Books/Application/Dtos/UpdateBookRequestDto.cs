namespace LibrarySystem.Modules.Books.Application.Dtos;

public sealed record UpdateBookRequestDto(
    string Name,
    string Author,
    int Stock,
    string Category,
    string? Description = null,
    string? Isbn = null,
    string? Publisher = null,
    int? PublishedYear = null);

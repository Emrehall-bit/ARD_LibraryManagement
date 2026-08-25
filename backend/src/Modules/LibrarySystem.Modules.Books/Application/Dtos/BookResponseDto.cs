namespace LibrarySystem.Modules.Books.Application.Dtos;

public sealed record BookResponseDto(
    Guid Id,
    string Name,
    string Author,
    int Stock,
    string Category,
    string? Description,
    string? Isbn,
    string? Publisher,
    int? PublishedYear,
    string? CoverImageUrl);

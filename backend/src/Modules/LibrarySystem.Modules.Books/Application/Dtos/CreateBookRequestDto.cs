namespace LibrarySystem.Modules.Books.Application.Dtos;

public sealed record CreateBookRequestDto(
    string Name,
    string Author,
    int Stock,
    string Category);

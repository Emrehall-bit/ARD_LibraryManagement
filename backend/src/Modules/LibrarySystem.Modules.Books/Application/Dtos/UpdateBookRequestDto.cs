namespace LibrarySystem.Modules.Books.Application.Dtos;

public sealed record UpdateBookRequestDto(
    string Name,
    string Author,
    int Stock);

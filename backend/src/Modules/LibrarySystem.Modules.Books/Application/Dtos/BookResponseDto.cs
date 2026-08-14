namespace LibrarySystem.Modules.Books.Application.Dtos;

public sealed record BookResponseDto(
    Guid Id,
    string Name,
    string Author,
    int Stock);

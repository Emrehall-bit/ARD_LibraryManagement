namespace LibrarySystem.Modules.Books.Application.Contracts;

public sealed record BookLookupItem(
    Guid Id,
    string Name,
    string Author);

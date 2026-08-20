using LibrarySystem.Modules.Books.Domain;

namespace LibrarySystem.Modules.Books.Application.Models;

public sealed record BookPage(
    IReadOnlyList<Book> Items,
    int Page,
    int PageSize,
    int TotalCount);

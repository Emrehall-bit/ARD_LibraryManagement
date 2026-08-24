namespace LibrarySystem.Modules.Books.Application.Contracts;

public sealed record BookCatalogSummary(
    int TotalBooks,
    int TotalStock,
    int OutOfStockBooks);

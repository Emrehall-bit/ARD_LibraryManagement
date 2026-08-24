namespace LibrarySystem.Modules.Books.Application.Dtos;

public sealed class GetBooksQueryDto
{
    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 20;

    public string? Search { get; init; }

    public string SortBy { get; init; } = "name";

    public string SortDirection { get; init; } = "asc";

    public string StockStatus { get; init; } = "all";

    public string? Category { get; init; }
}

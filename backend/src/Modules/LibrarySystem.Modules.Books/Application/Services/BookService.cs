using FluentValidation;
using LibrarySystem.Modules.Books.Application.Dtos;
using LibrarySystem.Modules.Books.Application.Interfaces;
using LibrarySystem.Modules.Books.Domain;
using LibrarySystem.Shared.Exceptions;

namespace LibrarySystem.Modules.Books.Application.Services;

internal sealed class BookService(
    IBookRepository bookRepository,
    IValidator<GetBooksQueryDto> getBooksQueryValidator,
    IValidator<CreateBookRequestDto> createBookRequestValidator,
    IValidator<UpdateBookRequestDto> updateBookRequestValidator) : IBookService
{
    public async Task<PagedBooksResponseDto> GetAllAsync(
        GetBooksQueryDto query,
        CancellationToken cancellationToken = default)
    {
        await getBooksQueryValidator.ValidateAndThrowAsync(query, cancellationToken);

        var trimmedSearch = string.IsNullOrWhiteSpace(query.Search)
            ? null
            : query.Search.Trim();
        var page = await bookRepository.GetPageAsync(
            query.Page,
            query.PageSize,
            trimmedSearch,
            NormalizeQueryValue(query.SortBy),
            NormalizeQueryValue(query.SortDirection),
            NormalizeQueryValue(query.StockStatus),
            ParseOptionalCategory(query.Category),
            cancellationToken);
        var totalPages = page.TotalCount == 0
            ? 0
            : (int)Math.Ceiling(page.TotalCount / (double)page.PageSize);

        return new PagedBooksResponseDto(
            page.Items.Select(MapToResponseDto).ToList(),
            page.Page,
            page.PageSize,
            page.TotalCount,
            totalPages);
    }

    public async Task<BookDetailResponseDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var book = await GetBookOrThrowAsync(id, cancellationToken);

        return MapToDetailResponseDto(book);
    }

    public async Task<BookResponseDto> CreateAsync(
        CreateBookRequestDto request,
        CancellationToken cancellationToken = default)
    {
        await createBookRequestValidator.ValidateAndThrowAsync(request, cancellationToken);

        var book = new Book(
            Guid.NewGuid(),
            request.Name,
            request.Author,
            request.Stock,
            ParseCategory(request.Category),
            request.Description,
            request.Isbn,
            request.Publisher,
            request.PublishedYear);

        await bookRepository.AddAsync(book, cancellationToken);
        await bookRepository.SaveChangesAsync(cancellationToken);

        return MapToResponseDto(book);
    }

    public async Task<BookResponseDto> UpdateAsync(
        Guid id,
        UpdateBookRequestDto request,
        CancellationToken cancellationToken = default)
    {
        await updateBookRequestValidator.ValidateAndThrowAsync(request, cancellationToken);

        var book = await GetTrackedBookOrThrowAsync(id, cancellationToken);

        book.Update(
            request.Name,
            request.Author,
            request.Stock,
            ParseCategory(request.Category),
            request.Description,
            request.Isbn,
            request.Publisher,
            request.PublishedYear);
        await bookRepository.SaveChangesAsync(cancellationToken);

        return MapToResponseDto(book);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var book = await GetBookOrThrowAsync(id, cancellationToken);

        await bookRepository.DeleteAsync(book, cancellationToken);
        await bookRepository.SaveChangesAsync(cancellationToken);
    }

    private static string NormalizeQueryValue(string value)
    {
        return value.Trim().ToLowerInvariant();
    }

    private static BookCategory? ParseOptionalCategory(string? category)
    {
        return string.IsNullOrWhiteSpace(category)
            ? null
            : ParseCategory(category);
    }

    private static BookCategory ParseCategory(string category)
    {
        return Enum.Parse<BookCategory>(category.Trim(), ignoreCase: true);
    }

    private async Task<Book> GetBookOrThrowAsync(Guid id, CancellationToken cancellationToken)
    {
        var book = await bookRepository.GetByIdAsync(id, cancellationToken);

        return book ?? throw new NotFoundException($"Book with id '{id}' was not found.");
    }

    private async Task<Book> GetTrackedBookOrThrowAsync(Guid id, CancellationToken cancellationToken)
    {
        var book = await bookRepository.GetTrackedByIdAsync(id, cancellationToken);

        return book ?? throw new NotFoundException($"Book with id '{id}' was not found.");
    }

    private static BookResponseDto MapToResponseDto(Book book)
    {
        return new BookResponseDto(
            book.Id,
            book.Name,
            book.Author,
            book.Stock,
            book.Category.ToString(),
            book.Description,
            book.Isbn,
            book.Publisher,
            book.PublishedYear);
    }

    private static BookDetailResponseDto MapToDetailResponseDto(Book book)
    {
        return new BookDetailResponseDto(
            book.Id,
            book.Name,
            book.Author,
            book.Stock,
            book.Category.ToString(),
            book.Description,
            book.Isbn,
            book.Publisher,
            book.PublishedYear,
            book.Images
                .OrderByDescending(image => image.IsCover)
                .ThenBy(image => image.SortOrder)
                .ThenBy(image => image.Id)
                .Select(MapToImageResponseDto)
                .ToList());
    }

    private static BookImageResponseDto MapToImageResponseDto(BookImage image)
    {
        return new BookImageResponseDto(
            image.Id,
            image.ObjectName,
            image.IsCover,
            image.SortOrder);
    }
}

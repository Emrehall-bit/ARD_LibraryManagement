using LibrarySystem.Modules.Books.Application.Dtos;
using LibrarySystem.Modules.Books.Application.Interfaces;
using LibrarySystem.Modules.Books.Domain;

namespace LibrarySystem.Modules.Books.Application.Services;

internal sealed class BookService(IBookRepository bookRepository) : IBookService
{
    public async Task<IReadOnlyList<BookResponseDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var books = await bookRepository.GetAllAsync(cancellationToken);

        return books.Select(MapToResponseDto).ToList();
    }

    public async Task<BookResponseDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var book = await GetBookOrThrowAsync(id, cancellationToken);

        return MapToResponseDto(book);
    }

    public async Task<BookResponseDto> CreateAsync(
        CreateBookRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var book = new Book(Guid.NewGuid(), request.Name, request.Author, request.Stock);

        await bookRepository.AddAsync(book, cancellationToken);
        await bookRepository.SaveChangesAsync(cancellationToken);

        return MapToResponseDto(book);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var book = await GetBookOrThrowAsync(id, cancellationToken);

        await bookRepository.DeleteAsync(book, cancellationToken);
        await bookRepository.SaveChangesAsync(cancellationToken);
    }

    private async Task<Book> GetBookOrThrowAsync(Guid id, CancellationToken cancellationToken)
    {
        var book = await bookRepository.GetByIdAsync(id, cancellationToken);

        return book ?? throw new KeyNotFoundException($"Book with id '{id}' was not found.");
    }

    private static BookResponseDto MapToResponseDto(Book book)
    {
        return new BookResponseDto(
            book.Id,
            book.Name,
            book.Author,
            book.Stock);
    }
}

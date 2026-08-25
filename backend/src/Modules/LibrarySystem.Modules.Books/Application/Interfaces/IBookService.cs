using LibrarySystem.Modules.Books.Application.Dtos;

namespace LibrarySystem.Modules.Books.Application.Interfaces;

public interface IBookService
{
    Task<PagedBooksResponseDto> GetAllAsync(
        GetBooksQueryDto query,
        CancellationToken cancellationToken = default);

    Task<BookDetailResponseDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<BookResponseDto> CreateAsync(CreateBookRequestDto request, CancellationToken cancellationToken = default);

    Task<BookResponseDto> UpdateAsync(
        Guid id,
        UpdateBookRequestDto request,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

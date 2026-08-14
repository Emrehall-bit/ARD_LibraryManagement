using LibrarySystem.Modules.Books.Application.Dtos;

namespace LibrarySystem.Modules.Books.Application.Interfaces;

public interface IBookService
{
    Task<IReadOnlyList<BookResponseDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<BookResponseDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<BookResponseDto> CreateAsync(CreateBookRequestDto request, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

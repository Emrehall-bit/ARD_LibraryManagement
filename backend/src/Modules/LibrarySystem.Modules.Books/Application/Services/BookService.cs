using FluentValidation;
using LibrarySystem.Modules.Books.Application.Contracts;
using LibrarySystem.Modules.Books.Application.Dtos;
using LibrarySystem.Modules.Books.Application.Interfaces;
using LibrarySystem.Modules.Books.Domain;
using LibrarySystem.Shared.Exceptions;
using Microsoft.Extensions.Logging;

namespace LibrarySystem.Modules.Books.Application.Services;

internal sealed class BookService(
    IBookRepository bookRepository,
    IBookImageStorage bookImageStorage,
    ILogger<BookService> logger,
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
        var coverImageUrls = await GetCoverImageUrlsByBookIdsAsync(
            page.Items.Select(book => book.Id).ToList(),
            cancellationToken);

        return new PagedBooksResponseDto(
            page.Items
                .Select(book => MapToResponseDto(
                    book,
                    coverImageUrls.GetValueOrDefault(book.Id)))
                .ToList(),
            page.Page,
            page.PageSize,
            page.TotalCount,
            totalPages);
    }

    public async Task<BookDetailResponseDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var book = await GetBookOrThrowAsync(id, cancellationToken);

        return await MapToDetailResponseDtoAsync(book, cancellationToken);
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

        return MapToResponseDto(book, coverImageUrl: null);
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

        return MapToResponseDto(book, coverImageUrl: null);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var book = await GetTrackedBookWithImagesOrThrowAsync(id, cancellationToken);
        var objectNames = book.Images
            .Select(image => image.ObjectName)
            .ToList();

        await bookRepository.DeleteAsync(book, cancellationToken);
        await bookRepository.SaveChangesAsync(cancellationToken);

        foreach (var objectName in objectNames)
        {
            await DeleteStorageObjectBestEffortAsync(objectName, cancellationToken);
        }
    }

    public async Task<BookImageResponseDto> UploadImageAsync(
        Guid bookId,
        UploadBookImageRequestDto request,
        CancellationToken cancellationToken = default)
    {
        _ = await GetTrackedBookOrThrowAsync(bookId, cancellationToken);

        var imageCount = await bookRepository.CountImagesByBookIdAsync(bookId, cancellationToken);
        if (imageCount >= BookImagePolicy.MaxImagesPerBook)
        {
            throw new BusinessException("Book image limit has been reached.");
        }

        ValidateUploadRequest(request);

        var imageId = Guid.NewGuid();
        var objectName = CreateObjectName(bookId, imageId, request.ContentType);
        var makeCover = request.IsCover || imageCount == 0;
        var sortOrder = request.SortOrder ?? imageCount;
        var image = new BookImage(imageId, bookId, objectName, makeCover, sortOrder);

        await bookImageStorage.UploadAsync(
            objectName,
            request.Content,
            request.ContentType,
            request.Size,
            cancellationToken);

        try
        {
            await bookRepository.AddImageAsync(image, makeCover, cancellationToken);
        }
        catch (Exception exception)
        {
            await DeleteUploadedObjectAfterFailureAsync(objectName, exception, cancellationToken);
            throw;
        }

        return await MapToImageResponseDtoAsync(image, cancellationToken);
    }

    public async Task<BookImageResponseDto> SetCoverAsync(
        Guid bookId,
        Guid imageId,
        CancellationToken cancellationToken = default)
    {
        var image = await bookRepository.GetImageByIdAndBookIdAsync(bookId, imageId, cancellationToken);
        if (image is null)
        {
            throw new NotFoundException("Book image not found.");
        }

        if (!await bookRepository.SetCoverAsync(bookId, imageId, cancellationToken))
        {
            throw new NotFoundException("Book image not found.");
        }

        image.SetCover(true);

        return await MapToImageResponseDtoAsync(image, cancellationToken);
    }

    public async Task DeleteImageAsync(
        Guid bookId,
        Guid imageId,
        CancellationToken cancellationToken = default)
    {
        var image = await bookRepository.DeleteImageAsync(bookId, imageId, cancellationToken);
        if (image is null)
        {
            throw new NotFoundException("Book image not found.");
        }

        await DeleteStorageObjectBestEffortAsync(image.ObjectName, cancellationToken);
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

    private async Task<Book> GetTrackedBookWithImagesOrThrowAsync(Guid id, CancellationToken cancellationToken)
    {
        var book = await bookRepository.GetTrackedByIdWithImagesAsync(id, cancellationToken);

        return book ?? throw new NotFoundException($"Book with id '{id}' was not found.");
    }

    private static BookResponseDto MapToResponseDto(Book book, string? coverImageUrl)
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
            book.PublishedYear,
            coverImageUrl);
    }

    private async Task<IReadOnlyDictionary<Guid, string>> GetCoverImageUrlsByBookIdsAsync(
        IReadOnlyCollection<Guid> bookIds,
        CancellationToken cancellationToken)
    {
        var coverObjectNames = await bookRepository.GetCoverObjectNamesByBookIdsAsync(bookIds, cancellationToken);
        var coverImageUrls = new Dictionary<Guid, string>();

        foreach (var (bookId, objectName) in coverObjectNames)
        {
            var url = await TryGetCoverImageUrlAsync(bookId, objectName, cancellationToken);
            if (url is not null)
            {
                coverImageUrls[bookId] = url;
            }
        }

        return coverImageUrls;
    }

    private async Task<string?> TryGetCoverImageUrlAsync(
        Guid bookId,
        string objectName,
        CancellationToken cancellationToken)
    {
        try
        {
            return await bookImageStorage.GetReadUrlAsync(
                objectName,
                BookImageStorageDefaults.DefaultReadUrlExpiry,
                cancellationToken);
        }
        catch (ObjectStorageException exception)
        {
            logger.LogWarning(
                exception,
                "Failed to create cover image URL for book '{BookId}' and object '{ObjectName}'.",
                bookId,
                objectName);

            return null;
        }
    }

    private async Task<BookDetailResponseDto> MapToDetailResponseDtoAsync(
        Book book,
        CancellationToken cancellationToken)
    {
        var images = new List<BookImageResponseDto>();
        foreach (var image in book.Images
            .OrderByDescending(image => image.IsCover)
            .ThenBy(image => image.SortOrder)
            .ThenBy(image => image.Id))
        {
            images.Add(await MapToImageResponseDtoAsync(image, cancellationToken));
        }

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
            images);
    }

    private async Task<BookImageResponseDto> MapToImageResponseDtoAsync(
        BookImage image,
        CancellationToken cancellationToken)
    {
        var url = await bookImageStorage.GetReadUrlAsync(
            image.ObjectName,
            BookImageStorageDefaults.DefaultReadUrlExpiry,
            cancellationToken);

        return new BookImageResponseDto(
            image.Id,
            url,
            image.IsCover,
            image.SortOrder);
    }

    private static void ValidateUploadRequest(UploadBookImageRequestDto request)
    {
        if (request.Content is null)
        {
            throw new BusinessException("Image file is required.");
        }

        if (request.Size == 0)
        {
            throw new BusinessException("Image file is empty.");
        }

        if (request.Size > BookImagePolicy.MaxImageSizeBytes)
        {
            throw new BusinessException("Image file exceeds the maximum allowed size.");
        }

        if (!BookImagePolicy.SupportedContentTypes.Contains(request.ContentType))
        {
            throw new BusinessException("Unsupported image content type.");
        }

        if (request.SortOrder < 0)
        {
            throw new BusinessException("Sort order cannot be negative.");
        }
    }

    private static string CreateObjectName(Guid bookId, Guid imageId, string contentType)
    {
        var extension = contentType.ToLowerInvariant() switch
        {
            "image/jpeg" => "jpg",
            "image/png" => "png",
            "image/webp" => "webp",
            _ => throw new BusinessException("Unsupported image content type.")
        };

        return $"books/{bookId}/{imageId:N}.{extension}";
    }

    private async Task DeleteUploadedObjectAfterFailureAsync(
        string objectName,
        Exception originalException,
        CancellationToken cancellationToken)
    {
        try
        {
            await bookImageStorage.DeleteAsync(objectName, cancellationToken);
        }
        catch (Exception cleanupException)
        {
            logger.LogWarning(
                cleanupException,
                "Failed to delete uploaded book image object '{ObjectName}' after database failure: {FailureType}.",
                objectName,
                originalException.GetType().Name);
        }
    }

    private async Task DeleteStorageObjectBestEffortAsync(
        string objectName,
        CancellationToken cancellationToken)
    {
        try
        {
            await bookImageStorage.DeleteAsync(objectName, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Failed to delete book image object '{ObjectName}' from storage after database commit.",
                objectName);
        }
    }
}

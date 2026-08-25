namespace LibrarySystem.Modules.Books.Application.Dtos;

public sealed record UploadBookImageRequestDto(
    Stream Content,
    string ContentType,
    long Size,
    bool IsCover,
    int? SortOrder);

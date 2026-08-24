namespace LibrarySystem.Modules.Borrowing.Application.Dtos;

public sealed class GetOverdueBorrowRecordsQueryDto
{
    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 20;
}

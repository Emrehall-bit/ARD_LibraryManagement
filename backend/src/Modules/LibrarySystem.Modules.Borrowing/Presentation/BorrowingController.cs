using LibrarySystem.Modules.Borrowing.Application.Dtos;
using LibrarySystem.Modules.Borrowing.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace LibrarySystem.Modules.Borrowing.Presentation;

[ApiController]
[Authorize]
[Route("api/borrow")]
public sealed class BorrowingController(IBorrowingService borrowingService) : ControllerBase
{
    [HttpPost("{bookId:guid}")]
    [ProducesResponseType(typeof(BorrowRecordResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BorrowRecordResponseDto>> BorrowBook(
        Guid bookId,
        CancellationToken cancellationToken)
    {
        var borrowRecord = await borrowingService.BorrowBookAsync(bookId, cancellationToken);

        return Ok(borrowRecord);
    }

    [HttpPost("~/api/return/{bookId:guid}")]
    [ProducesResponseType(typeof(BorrowRecordResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BorrowRecordResponseDto>> ReturnBook(
        Guid bookId,
        CancellationToken cancellationToken)
    {
        var borrowRecord = await borrowingService.ReturnBookAsync(bookId, cancellationToken);

        return Ok(borrowRecord);
    }

    [HttpPost("renew/{bookId:guid}")]
    [ProducesResponseType(typeof(BorrowRecordResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BorrowRecordResponseDto>> RenewBook(
        Guid bookId,
        CancellationToken cancellationToken)
    {
        var borrowRecord = await borrowingService.RenewBookAsync(bookId, cancellationToken);

        return Ok(borrowRecord);
    }

    [HttpGet("my-books")]
    [ProducesResponseType(typeof(IReadOnlyList<BorrowRecordResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<BorrowRecordResponseDto>>> GetMyBooks(
        CancellationToken cancellationToken)
    {
        var borrowRecords = await borrowingService.GetMyBooksAsync(cancellationToken);

        return Ok(borrowRecords);
    }

    [HttpGet("history")]
    [ProducesResponseType(typeof(IReadOnlyList<BorrowRecordResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<BorrowRecordResponseDto>>> GetHistory(
        CancellationToken cancellationToken)
    {
        var borrowRecords = await borrowingService.GetHistoryAsync(cancellationToken);

        return Ok(borrowRecords);
    }
}

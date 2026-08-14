using LibrarySystem.Modules.Books.Application.Dtos;
using LibrarySystem.Modules.Books.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace LibrarySystem.Modules.Books.Presentation;

[ApiController]
[Route("api/books")]
public sealed class BooksController(IBookService bookService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<BookResponseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BookResponseDto>>> GetAll(
        CancellationToken cancellationToken)
    {
        var books = await bookService.GetAllAsync(cancellationToken);

        return Ok(books);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(BookResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookResponseDto>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var book = await bookService.GetByIdAsync(id, cancellationToken);

        return Ok(book);
    }

    [HttpPost]
    [ProducesResponseType(typeof(BookResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BookResponseDto>> Create(
        CreateBookRequestDto request,
        CancellationToken cancellationToken)
    {
        var book = await bookService.CreateAsync(request, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = book.Id }, book);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await bookService.DeleteAsync(id, cancellationToken);

        return NoContent();
    }
}

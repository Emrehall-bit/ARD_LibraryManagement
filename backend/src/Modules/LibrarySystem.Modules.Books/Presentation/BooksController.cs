using LibrarySystem.Modules.Books.Application.Dtos;
using LibrarySystem.Modules.Books.Application.Interfaces;
using LibrarySystem.Shared.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace LibrarySystem.Modules.Books.Presentation;

[ApiController]
[Route("api/books")]
public sealed class BooksController(IBookService bookService) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PagedBooksResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedBooksResponseDto>> GetAll(
        [FromQuery] GetBooksQueryDto query,
        CancellationToken cancellationToken)
    {
        var books = await bookService.GetAllAsync(query, cancellationToken);

        return Ok(books);
    }

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(BookDetailResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookDetailResponseDto>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var book = await bookService.GetByIdAsync(id, cancellationToken);

        return Ok(book);
    }

    [HttpPost]
    [Authorize(Roles = IdentityRoles.Admin)]
    [ProducesResponseType(typeof(BookResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BookResponseDto>> Create(
        CreateBookRequestDto request,
        CancellationToken cancellationToken)
    {
        var book = await bookService.CreateAsync(request, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = book.Id }, book);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = IdentityRoles.Admin)]
    [ProducesResponseType(typeof(BookResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookResponseDto>> Update(
        Guid id,
        UpdateBookRequestDto request,
        CancellationToken cancellationToken)
    {
        var book = await bookService.UpdateAsync(id, request, cancellationToken);

        return Ok(book);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = IdentityRoles.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await bookService.DeleteAsync(id, cancellationToken);

        return NoContent();
    }
}

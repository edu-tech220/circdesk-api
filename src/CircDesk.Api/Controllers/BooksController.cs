using CircDesk.Api.Dtos;
using CircDesk.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CircDesk.Api.Controllers;

[ApiController]
[Route("api/books")]
[Authorize]
public class BooksController : ControllerBase
{
    private readonly IBookService _bookService;

    public BooksController(IBookService bookService)
    {
        _bookService = bookService;
    }

    [HttpGet]
    public async Task<ActionResult<Common.PagedResult<BookResponse>>> Search(
        [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        return Ok(await _bookService.SearchAsync(search, page, pageSize, ct));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<BookResponse>> GetById(int id, CancellationToken ct)
    {
        return Ok(await _bookService.GetByIdAsync(id, ct));
    }

    [HttpPost]
    [Authorize(Roles = nameof(Domain.MemberRole.Librarian))]
    public async Task<ActionResult<BookResponse>> Create(CreateBookRequest request, CancellationToken ct)
    {
        var book = await _bookService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = book.Id }, book);
    }

    [HttpPost("{id:int}/copies")]
    [Authorize(Roles = nameof(Domain.MemberRole.Librarian))]
    public async Task<ActionResult<CopyResponse>> AddCopy(int id, AddCopyRequest request, CancellationToken ct)
    {
        return Ok(await _bookService.AddCopyAsync(id, request, ct));
    }
}

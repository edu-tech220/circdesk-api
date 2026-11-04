using System.Linq.Expressions;
using CircDesk.Api.Common;
using CircDesk.Api.Data;
using CircDesk.Api.Domain;
using CircDesk.Api.Dtos;
using Microsoft.EntityFrameworkCore;

namespace CircDesk.Api.Services;

public class BookService : IBookService
{
    private readonly CircDeskDbContext _db;

    public BookService(CircDeskDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResult<BookResponse>> SearchAsync(string? search, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.Books.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(b => EF.Functions.Like(b.Title, $"%{term}%") || EF.Functions.Like(b.Author, $"%{term}%"));
        }

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderBy(b => b.Title)
            .ThenBy(b => b.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(ToResponseExpression)
            .ToListAsync(ct);

        return new PagedResult<BookResponse>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public async Task<BookResponse> GetByIdAsync(int bookId, CancellationToken ct = default)
    {
        var book = await _db.Books.AsNoTracking()
            .Where(b => b.Id == bookId)
            .Select(ToResponseExpression)
            .FirstOrDefaultAsync(ct);

        return book ?? throw new NotFoundException($"No existe un libro con id {bookId}.");
    }

    public async Task<BookResponse> CreateAsync(CreateBookRequest request, CancellationToken ct = default)
    {
        var book = new Book
        {
            Title = request.Title.Trim(),
            Author = request.Author.Trim(),
            Isbn = request.Isbn.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        for (var i = 1; i <= request.InitialCopies; i++)
        {
            book.Copies.Add(new Copy
            {
                InventoryCode = $"{request.Isbn.Trim()}-{i:D3}",
                Status = CopyStatus.Available
            });
        }

        _db.Books.Add(book);
        await _db.SaveChangesAsync(ct);

        return await GetByIdAsync(book.Id, ct);
    }

    public async Task<CopyResponse> AddCopyAsync(int bookId, AddCopyRequest request, CancellationToken ct = default)
    {
        var book = await _db.Books.Include(b => b.Copies).FirstOrDefaultAsync(b => b.Id == bookId, ct)
            ?? throw new NotFoundException($"No existe un libro con id {bookId}.");

        var inventoryCode = request.InventoryCode.Trim();
        if (await _db.Copies.AnyAsync(c => c.InventoryCode == inventoryCode, ct))
        {
            throw new ConflictException($"Ya existe un ejemplar con el código '{inventoryCode}'.");
        }

        var copy = new Copy
        {
            BookId = book.Id,
            InventoryCode = inventoryCode,
            Status = CopyStatus.Available
        };

        _db.Copies.Add(copy);
        await _db.SaveChangesAsync(ct);

        return new CopyResponse(copy.Id, copy.InventoryCode, copy.Status.ToString());
    }

    // An Expression (not a plain method) so EF Core can translate the Copies/Holds counts into
    // correlated SQL subqueries instead of materializing full entities to evaluate client-side.
    private static readonly Expression<Func<Book, BookResponse>> ToResponseExpression = b => new BookResponse(
        b.Id,
        b.Title,
        b.Author,
        b.Isbn,
        b.Copies.Count,
        b.Copies.Count(c => c.Status == CopyStatus.Available),
        b.Holds.Count(h => h.Status == HoldStatus.Queued || h.Status == HoldStatus.Ready));
}

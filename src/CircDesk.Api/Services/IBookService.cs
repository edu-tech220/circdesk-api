using CircDesk.Api.Common;
using CircDesk.Api.Dtos;

namespace CircDesk.Api.Services;

public interface IBookService
{
    Task<PagedResult<BookResponse>> SearchAsync(string? search, int page, int pageSize, CancellationToken ct = default);
    Task<BookResponse> GetByIdAsync(int bookId, CancellationToken ct = default);
    Task<BookResponse> CreateAsync(CreateBookRequest request, CancellationToken ct = default);
    Task<CopyResponse> AddCopyAsync(int bookId, AddCopyRequest request, CancellationToken ct = default);
}

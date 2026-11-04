using CircDesk.Api.Common;
using CircDesk.Api.Dtos;

namespace CircDesk.Api.Services;

public interface IHoldService
{
    Task<HoldResponse> CreateAsync(int memberId, int bookId, CancellationToken ct = default);
    Task<HoldResponse> CancelAsync(int holdId, int memberId, bool isLibrarian, CancellationToken ct = default);
    Task<PagedResult<HoldResponse>> ListMineAsync(int memberId, int page, int pageSize, CancellationToken ct = default);

    /// <summary>
    /// Called when a copy becomes free (return, hold cancellation, expiration). Promotes the
    /// oldest queued hold for the book onto that copy, or leaves the copy Available if the
    /// queue is empty.
    /// </summary>
    Task ReleaseCopyAsync(int bookId, int copyId, CancellationToken ct = default);

    /// <summary>Expires Ready holds past their pickup deadline and cascades to the next hold in line.</summary>
    Task<int> ExpireDueHoldsAsync(CancellationToken ct = default);
}

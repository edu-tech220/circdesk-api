using CircDesk.Api.Common;
using CircDesk.Api.Dtos;

namespace CircDesk.Api.Services;

public interface IFineService
{
    Task<PagedResult<FineResponse>> ListMineAsync(int memberId, int page, int pageSize, CancellationToken ct = default);
    Task<FineResponse> PayAsync(int fineId, int memberId, bool isLibrarian, CancellationToken ct = default);
}

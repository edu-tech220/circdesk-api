using CircDesk.Api.Common;
using CircDesk.Api.Dtos;

namespace CircDesk.Api.Services;

public interface ILoanService
{
    Task<LoanResponse> BorrowAsync(int memberId, int bookId, CancellationToken ct = default);
    Task<ReturnResponse> ReturnAsync(int loanId, CancellationToken ct = default);
    Task<LoanResponse> RenewAsync(int loanId, int memberId, bool isLibrarian, CancellationToken ct = default);
    Task<PagedResult<LoanResponse>> ListMineAsync(int memberId, int page, int pageSize, CancellationToken ct = default);
    Task<PagedResult<LoanResponse>> ListAllAsync(int? memberId, bool? overdueOnly, int page, int pageSize, CancellationToken ct = default);
}

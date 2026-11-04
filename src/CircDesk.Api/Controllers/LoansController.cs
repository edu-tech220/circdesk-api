using CircDesk.Api.Common;
using CircDesk.Api.Dtos;
using CircDesk.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CircDesk.Api.Controllers;

[ApiController]
[Route("api/loans")]
[Authorize]
public class LoansController : ControllerBase
{
    private readonly ILoanService _loanService;

    public LoansController(ILoanService loanService)
    {
        _loanService = loanService;
    }

    [HttpPost]
    public async Task<ActionResult<LoanResponse>> Borrow(BorrowRequest request, CancellationToken ct)
    {
        var memberId = User.GetMemberId();
        var loan = await _loanService.BorrowAsync(memberId, request.BookId, ct);
        return Created($"/api/loans/{loan.Id}", loan);
    }

    [HttpPost("{id:int}/return")]
    [Authorize(Roles = nameof(Domain.MemberRole.Librarian))]
    public async Task<ActionResult<ReturnResponse>> Return(int id, CancellationToken ct)
    {
        return Ok(await _loanService.ReturnAsync(id, ct));
    }

    [HttpPost("{id:int}/renew")]
    public async Task<ActionResult<LoanResponse>> Renew(int id, CancellationToken ct)
    {
        var memberId = User.GetMemberId();
        return Ok(await _loanService.RenewAsync(id, memberId, User.IsLibrarian(), ct));
    }

    [HttpGet("mine")]
    public async Task<ActionResult<PagedResult<LoanResponse>>> GetMine([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var memberId = User.GetMemberId();
        return Ok(await _loanService.ListMineAsync(memberId, page, pageSize, ct));
    }

    [HttpGet]
    [Authorize(Roles = nameof(Domain.MemberRole.Librarian))]
    public async Task<ActionResult<PagedResult<LoanResponse>>> GetAll(
        [FromQuery] int? memberId, [FromQuery] bool? overdueOnly, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        return Ok(await _loanService.ListAllAsync(memberId, overdueOnly, page, pageSize, ct));
    }
}

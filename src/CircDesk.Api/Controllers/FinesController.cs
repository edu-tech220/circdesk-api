using CircDesk.Api.Common;
using CircDesk.Api.Dtos;
using CircDesk.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CircDesk.Api.Controllers;

[ApiController]
[Route("api/fines")]
[Authorize]
public class FinesController : ControllerBase
{
    private readonly IFineService _fineService;

    public FinesController(IFineService fineService)
    {
        _fineService = fineService;
    }

    [HttpGet("mine")]
    public async Task<ActionResult<PagedResult<FineResponse>>> GetMine([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var memberId = User.GetMemberId();
        return Ok(await _fineService.ListMineAsync(memberId, page, pageSize, ct));
    }

    [HttpPost("{id:int}/pay")]
    public async Task<ActionResult<FineResponse>> Pay(int id, CancellationToken ct)
    {
        var memberId = User.GetMemberId();
        return Ok(await _fineService.PayAsync(id, memberId, User.IsLibrarian(), ct));
    }
}

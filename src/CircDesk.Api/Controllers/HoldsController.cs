using CircDesk.Api.Common;
using CircDesk.Api.Dtos;
using CircDesk.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CircDesk.Api.Controllers;

[ApiController]
[Route("api/holds")]
[Authorize]
public class HoldsController : ControllerBase
{
    private readonly IHoldService _holdService;

    public HoldsController(IHoldService holdService)
    {
        _holdService = holdService;
    }

    [HttpPost]
    public async Task<ActionResult<HoldResponse>> Create(CreateHoldRequest request, CancellationToken ct)
    {
        var memberId = User.GetMemberId();
        var hold = await _holdService.CreateAsync(memberId, request.BookId, ct);
        return Created($"/api/holds/{hold.Id}", hold);
    }

    [HttpDelete("{id:int}")]
    public async Task<ActionResult<HoldResponse>> Cancel(int id, CancellationToken ct)
    {
        var memberId = User.GetMemberId();
        return Ok(await _holdService.CancelAsync(id, memberId, User.IsLibrarian(), ct));
    }

    [HttpGet("mine")]
    public async Task<ActionResult<PagedResult<HoldResponse>>> GetMine([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var memberId = User.GetMemberId();
        return Ok(await _holdService.ListMineAsync(memberId, page, pageSize, ct));
    }
}

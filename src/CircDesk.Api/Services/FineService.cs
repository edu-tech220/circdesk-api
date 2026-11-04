using CircDesk.Api.Common;
using CircDesk.Api.Data;
using CircDesk.Api.Domain;
using CircDesk.Api.Dtos;
using Microsoft.EntityFrameworkCore;

namespace CircDesk.Api.Services;

public class FineService : IFineService
{
    private readonly CircDeskDbContext _db;

    public FineService(CircDeskDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResult<FineResponse>> ListMineAsync(int memberId, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.Fines.AsNoTracking().Where(f => f.MemberId == memberId);
        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(f => f.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(f => f.Member)
            .Select(f => ToResponse(f))
            .ToListAsync(ct);

        return new PagedResult<FineResponse>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public async Task<FineResponse> PayAsync(int fineId, int memberId, bool isLibrarian, CancellationToken ct = default)
    {
        var fine = await _db.Fines.Include(f => f.Member).FirstOrDefaultAsync(f => f.Id == fineId, ct)
            ?? throw new NotFoundException($"No existe una multa con id {fineId}.");

        if (fine.MemberId != memberId && !isLibrarian)
        {
            throw new ForbiddenException("No puedes pagar la multa de otro socio.");
        }

        if (fine.Status == FineStatus.Paid)
        {
            throw new ConflictException("Esta multa ya fue pagada.");
        }

        fine.Status = FineStatus.Paid;
        fine.PaidAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return ToResponse(fine);
    }

    private static FineResponse ToResponse(Fine f) => new(
        f.Id, f.LoanId, f.MemberId, f.Member.Username, f.Amount, f.Status.ToString(), f.CreatedAt, f.PaidAt);
}

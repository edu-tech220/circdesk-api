using CircDesk.Api.Common;
using CircDesk.Api.Data;
using CircDesk.Api.Domain;
using CircDesk.Api.Dtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CircDesk.Api.Services;

public class HoldService : IHoldService
{
    private readonly CircDeskDbContext _db;
    private readonly CircDeskOptions _options;

    public HoldService(CircDeskDbContext db, IOptions<CircDeskOptions> options)
    {
        _db = db;
        _options = options.Value;
    }

    public async Task<HoldResponse> CreateAsync(int memberId, int bookId, CancellationToken ct = default)
    {
        var book = await _db.Books.Include(b => b.Copies).FirstOrDefaultAsync(b => b.Id == bookId, ct)
            ?? throw new NotFoundException($"No existe un libro con id {bookId}.");

        var hasAvailableCopy = book.Copies.Any(c => c.Status == CopyStatus.Available);
        if (hasAvailableCopy)
        {
            throw new ConflictException("Hay ejemplares disponibles; pide el préstamo directamente en vez de reservar.");
        }

        var alreadyQueued = await _db.Holds.AnyAsync(
            h => h.BookId == bookId && h.MemberId == memberId && (h.Status == HoldStatus.Queued || h.Status == HoldStatus.Ready), ct);
        if (alreadyQueued)
        {
            throw new ConflictException("Ya tienes una reserva activa para este libro.");
        }

        var activeLoanOnSameBook = await _db.Loans.AnyAsync(
            l => l.MemberId == memberId && l.Status != LoanStatus.Returned && l.Copy.BookId == bookId, ct);
        if (activeLoanOnSameBook)
        {
            throw new ConflictException("Ya tienes un préstamo activo de este libro.");
        }

        var hold = new Hold
        {
            BookId = bookId,
            MemberId = memberId,
            CreatedAt = DateTime.UtcNow,
            Status = HoldStatus.Queued
        };

        _db.Holds.Add(hold);
        await _db.SaveChangesAsync(ct);

        return await ToResponseAsync(hold, ct);
    }

    public async Task<HoldResponse> CancelAsync(int holdId, int memberId, bool isLibrarian, CancellationToken ct = default)
    {
        var hold = await _db.Holds.FirstOrDefaultAsync(h => h.Id == holdId, ct)
            ?? throw new NotFoundException($"No existe una reserva con id {holdId}.");

        if (hold.MemberId != memberId && !isLibrarian)
        {
            throw new ForbiddenException("No puedes cancelar la reserva de otro socio.");
        }

        if (hold.Status is not (HoldStatus.Queued or HoldStatus.Ready))
        {
            throw new ConflictException($"La reserva está en estado '{hold.Status}' y no se puede cancelar.");
        }

        var wasReady = hold.Status == HoldStatus.Ready;
        var reservedCopyId = hold.ReservedCopyId;
        var bookId = hold.BookId;

        hold.Status = HoldStatus.Cancelled;
        await _db.SaveChangesAsync(ct);

        if (wasReady && reservedCopyId.HasValue)
        {
            await ReleaseCopyAsync(bookId, reservedCopyId.Value, ct);
        }

        return await ToResponseAsync(hold, ct);
    }

    public async Task<PagedResult<HoldResponse>> ListMineAsync(int memberId, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.Holds.AsNoTracking().Where(h => h.MemberId == memberId);
        var totalCount = await query.CountAsync(ct);

        var holds = await query
            .OrderByDescending(h => h.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var items = new List<HoldResponse>(holds.Count);
        foreach (var hold in holds)
        {
            items.Add(await ToResponseAsync(hold, ct));
        }

        return new PagedResult<HoldResponse>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public async Task ReleaseCopyAsync(int bookId, int copyId, CancellationToken ct = default)
    {
        var copy = await _db.Copies.FirstOrDefaultAsync(c => c.Id == copyId, ct)
            ?? throw new NotFoundException($"No existe un ejemplar con id {copyId}.");

        var nextHold = await _db.Holds
            .Where(h => h.BookId == bookId && h.Status == HoldStatus.Queued)
            .OrderBy(h => h.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (nextHold is null)
        {
            copy.Status = CopyStatus.Available;
            await _db.SaveChangesAsync(ct);
            return;
        }

        nextHold.Status = HoldStatus.Ready;
        nextHold.ReadyAt = DateTime.UtcNow;
        nextHold.ExpiresAt = DateTime.UtcNow.AddHours(_options.HoldExpirationHours);
        nextHold.ReservedCopyId = copy.Id;
        copy.Status = CopyStatus.Reserved;

        await _db.SaveChangesAsync(ct);
    }

    public async Task<int> ExpireDueHoldsAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var expired = await _db.Holds
            .Where(h => h.Status == HoldStatus.Ready && h.ExpiresAt != null && h.ExpiresAt < now)
            .ToListAsync(ct);

        foreach (var hold in expired)
        {
            var bookId = hold.BookId;
            var copyId = hold.ReservedCopyId!.Value;

            hold.Status = HoldStatus.Expired;
            await _db.SaveChangesAsync(ct);

            await ReleaseCopyAsync(bookId, copyId, ct);
        }

        return expired.Count;
    }

    private async Task<HoldResponse> ToResponseAsync(Hold hold, CancellationToken ct)
    {
        var book = await _db.Books.AsNoTracking().FirstAsync(b => b.Id == hold.BookId, ct);
        var member = await _db.Members.AsNoTracking().FirstAsync(m => m.Id == hold.MemberId, ct);

        int? queuePosition = null;
        if (hold.Status == HoldStatus.Queued)
        {
            queuePosition = 1 + await _db.Holds.CountAsync(
                h => h.BookId == hold.BookId && h.Status == HoldStatus.Queued && h.CreatedAt < hold.CreatedAt, ct);
        }

        return new HoldResponse(
            hold.Id,
            hold.BookId,
            book.Title,
            hold.MemberId,
            member.Username,
            hold.CreatedAt,
            hold.Status.ToString(),
            queuePosition,
            hold.ReadyAt,
            hold.ExpiresAt);
    }
}

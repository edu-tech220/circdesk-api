using CircDesk.Api.Common;
using CircDesk.Api.Data;
using CircDesk.Api.Domain;
using CircDesk.Api.Dtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CircDesk.Api.Services;

public class LoanService : ILoanService
{
    private const int MaxCopyClaimAttempts = 5;

    private readonly CircDeskDbContext _db;
    private readonly IHoldService _holdService;
    private readonly CircDeskOptions _options;

    public LoanService(CircDeskDbContext db, IHoldService holdService, IOptions<CircDeskOptions> options)
    {
        _db = db;
        _holdService = holdService;
        _options = options.Value;
    }

    public async Task<LoanResponse> BorrowAsync(int memberId, int bookId, CancellationToken ct = default)
    {
        var member = await _db.Members.FindAsync(new object?[] { memberId }, ct)
            ?? throw new NotFoundException($"No existe un socio con id {memberId}.");

        if (member.IsBlocked)
        {
            throw new ForbiddenException("Tu cuenta está bloqueada por la biblioteca.");
        }

        // Summed client-side: SQLite has no native decimal aggregate, unlike SQL Server's SUM().
        var pendingFineAmounts = await _db.Fines
            .Where(f => f.MemberId == memberId && f.Status == FineStatus.Pending)
            .Select(f => f.Amount)
            .ToListAsync(ct);
        var outstandingFines = pendingFineAmounts.Sum();
        if (outstandingFines >= _options.OutstandingFineBlockThreshold)
        {
            throw new ForbiddenException($"Tienes ${outstandingFines:0.00} en multas pendientes; paga antes de pedir otro préstamo.");
        }

        var activeLoans = await _db.Loans.CountAsync(l => l.MemberId == memberId && l.Status != LoanStatus.Returned, ct);
        if (activeLoans >= _options.MaxActiveLoansPerMember)
        {
            throw new ConflictException($"Ya tienes {activeLoans} préstamos activos (máximo {_options.MaxActiveLoansPerMember}).");
        }

        var bookExists = await _db.Books.AnyAsync(b => b.Id == bookId, ct);
        if (!bookExists)
        {
            throw new NotFoundException($"No existe un libro con id {bookId}.");
        }

        var duplicateLoan = await _db.Loans.AnyAsync(
            l => l.MemberId == memberId && l.Status != LoanStatus.Returned && l.Copy.BookId == bookId, ct);
        if (duplicateLoan)
        {
            throw new ConflictException("Ya tienes un préstamo activo de este libro.");
        }

        var copy = await ClaimCopyAsync(memberId, bookId, ct);

        var loan = new Loan
        {
            CopyId = copy.Id,
            MemberId = memberId,
            LoanDate = DateTime.UtcNow,
            DueDate = DateTime.UtcNow.AddDays(_options.LoanPeriodDays),
            Status = LoanStatus.Active
        };

        _db.Loans.Add(loan);
        await _db.SaveChangesAsync(ct);

        return await ToResponseAsync(loan.Id, ct);
    }

    /// <summary>
    /// Fulfils a Ready hold for this member if one exists; otherwise claims the first Available
    /// copy with an optimistic-concurrency retry loop, since two members can race for the last
    /// copy of a book.
    /// </summary>
    private async Task<Copy> ClaimCopyAsync(int memberId, int bookId, CancellationToken ct)
    {
        var readyHold = await _db.Holds.FirstOrDefaultAsync(
            h => h.BookId == bookId && h.MemberId == memberId && h.Status == HoldStatus.Ready, ct);

        if (readyHold is not null)
        {
            if (readyHold.ExpiresAt is not null && readyHold.ExpiresAt < DateTime.UtcNow)
            {
                throw new ConflictException("Tu reserva ya expiró; puedes volver a reservar el libro.");
            }

            var reservedCopy = await _db.Copies.FirstAsync(c => c.Id == readyHold.ReservedCopyId!.Value, ct);
            readyHold.Status = HoldStatus.Fulfilled;
            reservedCopy.Status = CopyStatus.OnLoan;
            await _db.SaveChangesAsync(ct);
            return reservedCopy;
        }

        var attemptedIds = new List<int>();
        for (var attempt = 0; attempt < MaxCopyClaimAttempts; attempt++)
        {
            var candidate = await _db.Copies
                .Where(c => c.BookId == bookId && c.Status == CopyStatus.Available && !attemptedIds.Contains(c.Id))
                .OrderBy(c => c.Id)
                .FirstOrDefaultAsync(ct);

            if (candidate is null)
            {
                break;
            }

            candidate.Status = CopyStatus.OnLoan;
            try
            {
                await _db.SaveChangesAsync(ct);
                return candidate;
            }
            catch (DbUpdateConcurrencyException)
            {
                _db.Entry(candidate).State = EntityState.Detached;
                attemptedIds.Add(candidate.Id);
            }
        }

        throw new ConflictException("No hay ejemplares disponibles; crea una reserva para este libro.");
    }

    public async Task<ReturnResponse> ReturnAsync(int loanId, CancellationToken ct = default)
    {
        var loan = await _db.Loans.Include(l => l.Copy).FirstOrDefaultAsync(l => l.Id == loanId, ct)
            ?? throw new NotFoundException($"No existe un préstamo con id {loanId}.");

        if (loan.Status == LoanStatus.Returned)
        {
            throw new ConflictException("El préstamo ya fue devuelto.");
        }

        var now = DateTime.UtcNow;
        loan.ReturnDate = now;
        loan.Status = LoanStatus.Returned;

        decimal? fineAmount = null;
        if (now > loan.DueDate)
        {
            var daysLate = (int)Math.Ceiling((now - loan.DueDate).TotalDays);
            var amount = Math.Min(daysLate * _options.FinePerDayLate, _options.MaxFinePerLoan);
            fineAmount = amount;

            _db.Fines.Add(new Fine
            {
                MemberId = loan.MemberId,
                LoanId = loan.Id,
                Amount = amount,
                Status = FineStatus.Pending,
                CreatedAt = now
            });
        }

        var bookId = loan.Copy.BookId;
        var copyId = loan.CopyId;

        await _db.SaveChangesAsync(ct);
        await _holdService.ReleaseCopyAsync(bookId, copyId, ct);

        var response = await ToResponseAsync(loan.Id, ct);
        return new ReturnResponse(response, fineAmount);
    }

    public async Task<LoanResponse> RenewAsync(int loanId, int memberId, bool isLibrarian, CancellationToken ct = default)
    {
        var loan = await _db.Loans.Include(l => l.Copy).FirstOrDefaultAsync(l => l.Id == loanId, ct)
            ?? throw new NotFoundException($"No existe un préstamo con id {loanId}.");

        if (loan.MemberId != memberId && !isLibrarian)
        {
            throw new ForbiddenException("No puedes renovar el préstamo de otro socio.");
        }

        if (loan.Status == LoanStatus.Returned)
        {
            throw new ConflictException("El préstamo ya fue devuelto.");
        }

        var now = DateTime.UtcNow;
        if (loan.DueDate < now)
        {
            throw new ConflictException("No se puede renovar un préstamo vencido; primero debe devolverse.");
        }

        if (loan.RenewalCount >= _options.MaxRenewalsPerLoan)
        {
            throw new ConflictException($"Ya alcanzaste el máximo de {_options.MaxRenewalsPerLoan} renovación(es) para este préstamo.");
        }

        var hasPendingHold = await _db.Holds.AnyAsync(
            h => h.BookId == loan.Copy.BookId && (h.Status == HoldStatus.Queued || h.Status == HoldStatus.Ready), ct);
        if (hasPendingHold)
        {
            throw new ConflictException("No se puede renovar: hay otros socios esperando este libro.");
        }

        loan.DueDate = loan.DueDate.AddDays(_options.RenewalExtensionDays);
        loan.RenewalCount++;
        await _db.SaveChangesAsync(ct);

        return await ToResponseAsync(loan.Id, ct);
    }

    public async Task<PagedResult<LoanResponse>> ListMineAsync(int memberId, int page, int pageSize, CancellationToken ct = default)
    {
        return await ListAsync(_db.Loans.Where(l => l.MemberId == memberId), page, pageSize, ct);
    }

    public async Task<PagedResult<LoanResponse>> ListAllAsync(int? memberId, bool? overdueOnly, int page, int pageSize, CancellationToken ct = default)
    {
        var query = _db.Loans.AsQueryable();

        if (memberId.HasValue)
        {
            query = query.Where(l => l.MemberId == memberId.Value);
        }

        if (overdueOnly == true)
        {
            var now = DateTime.UtcNow;
            query = query.Where(l => l.Status != LoanStatus.Returned && l.DueDate < now);
        }

        return await ListAsync(query, page, pageSize, ct);
    }

    private async Task<PagedResult<LoanResponse>> ListAsync(IQueryable<Loan> query, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var totalCount = await query.CountAsync(ct);

        var ids = await query.AsNoTracking()
            .OrderByDescending(l => l.LoanDate)
            .ThenBy(l => l.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(l => l.Id)
            .ToListAsync(ct);

        var items = new List<LoanResponse>(ids.Count);
        foreach (var id in ids)
        {
            items.Add(await ToResponseAsync(id, ct));
        }

        return new PagedResult<LoanResponse>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    private async Task<LoanResponse> ToResponseAsync(int loanId, CancellationToken ct)
    {
        var loan = await _db.Loans.AsNoTracking()
            .Include(l => l.Copy).ThenInclude(c => c.Book)
            .Include(l => l.Member)
            .FirstAsync(l => l.Id == loanId, ct);

        var status = loan.Status == LoanStatus.Returned
            ? "Returned"
            : loan.DueDate < DateTime.UtcNow ? "Overdue" : "Active";

        return new LoanResponse(
            loan.Id,
            loan.Copy.BookId,
            loan.Copy.Book.Title,
            loan.CopyId,
            loan.Copy.InventoryCode,
            loan.MemberId,
            loan.Member.Username,
            loan.LoanDate,
            loan.DueDate,
            loan.ReturnDate,
            loan.RenewalCount,
            status);
    }
}

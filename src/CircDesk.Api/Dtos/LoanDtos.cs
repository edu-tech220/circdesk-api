using System.ComponentModel.DataAnnotations;

namespace CircDesk.Api.Dtos;

public record BorrowRequest([Required] int BookId);

public record LoanResponse(
    int Id,
    int BookId,
    string BookTitle,
    int CopyId,
    string CopyInventoryCode,
    int MemberId,
    string MemberUsername,
    DateTime LoanDate,
    DateTime DueDate,
    DateTime? ReturnDate,
    int RenewalCount,
    string Status);

public record ReturnResponse(LoanResponse Loan, decimal? FineCharged);

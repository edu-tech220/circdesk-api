namespace CircDesk.Api.Dtos;

public record FineResponse(
    int Id,
    int LoanId,
    int MemberId,
    string MemberUsername,
    decimal Amount,
    string Status,
    DateTime CreatedAt,
    DateTime? PaidAt);

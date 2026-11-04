namespace CircDesk.Api.Domain;

public class Fine
{
    public int Id { get; set; }
    public int MemberId { get; set; }
    public Member Member { get; set; } = null!;
    public int LoanId { get; set; }
    public Loan Loan { get; set; } = null!;

    public decimal Amount { get; set; }
    public FineStatus Status { get; set; } = FineStatus.Pending;
    public DateTime CreatedAt { get; set; }
    public DateTime? PaidAt { get; set; }
}

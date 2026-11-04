namespace CircDesk.Api.Domain;

public class Loan
{
    public int Id { get; set; }
    public int CopyId { get; set; }
    public Copy Copy { get; set; } = null!;
    public int MemberId { get; set; }
    public Member Member { get; set; } = null!;

    public DateTime LoanDate { get; set; }
    public DateTime DueDate { get; set; }
    public DateTime? ReturnDate { get; set; }
    public int RenewalCount { get; set; }
    public LoanStatus Status { get; set; } = LoanStatus.Active;

    public ICollection<Fine> Fines { get; set; } = new List<Fine>();
}

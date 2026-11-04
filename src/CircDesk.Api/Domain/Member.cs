namespace CircDesk.Api.Domain;

public class Member
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public byte[] PasswordHash { get; set; } = Array.Empty<byte>();
    public byte[] PasswordSalt { get; set; } = Array.Empty<byte>();
    public MemberRole Role { get; set; } = MemberRole.Member;
    public bool IsBlocked { get; set; }
    public DateTime CreatedAt { get; set; }

    public ICollection<Loan> Loans { get; set; } = new List<Loan>();
    public ICollection<Hold> Holds { get; set; } = new List<Hold>();
    public ICollection<Fine> Fines { get; set; } = new List<Fine>();
}

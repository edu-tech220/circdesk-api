namespace CircDesk.Api.Domain;

public class Hold
{
    public int Id { get; set; }
    public int BookId { get; set; }
    public Book Book { get; set; } = null!;
    public int MemberId { get; set; }
    public Member Member { get; set; } = null!;

    public DateTime CreatedAt { get; set; }
    public HoldStatus Status { get; set; } = HoldStatus.Queued;

    /// <summary>Set when a copy becomes available and the hold moves to the front.</summary>
    public DateTime? ReadyAt { get; set; }

    /// <summary>Deadline to pick up the reserved copy before it passes to the next hold.</summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>Copy set aside for this hold while it is Ready.</summary>
    public int? ReservedCopyId { get; set; }
}

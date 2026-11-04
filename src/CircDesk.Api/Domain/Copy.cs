namespace CircDesk.Api.Domain;

public class Copy : IHasRowVersion
{
    public int Id { get; set; }
    public int BookId { get; set; }
    public Book Book { get; set; } = null!;
    public string InventoryCode { get; set; } = string.Empty;
    public CopyStatus Status { get; set; } = CopyStatus.Available;

    public byte[] RowVersion { get; set; } = Guid.NewGuid().ToByteArray();
}

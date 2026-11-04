namespace CircDesk.Api.Domain;

public class Book
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Isbn { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public ICollection<Copy> Copies { get; set; } = new List<Copy>();
    public ICollection<Hold> Holds { get; set; } = new List<Hold>();
}

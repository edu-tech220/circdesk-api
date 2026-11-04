using System.ComponentModel.DataAnnotations;

namespace CircDesk.Api.Dtos;

public record CreateBookRequest(
    [Required, MaxLength(300)] string Title,
    [Required, MaxLength(200)] string Author,
    [Required, MaxLength(20)] string Isbn,
    [Range(0, 100)] int InitialCopies);

public record AddCopyRequest([Required, MaxLength(40)] string InventoryCode);

public record BookResponse(
    int Id,
    string Title,
    string Author,
    string Isbn,
    int TotalCopies,
    int AvailableCopies,
    int QueuedHolds);

public record CopyResponse(int Id, string InventoryCode, string Status);

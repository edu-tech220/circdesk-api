using System.ComponentModel.DataAnnotations;

namespace CircDesk.Api.Dtos;

public record CreateHoldRequest([Required] int BookId);

public record HoldResponse(
    int Id,
    int BookId,
    string BookTitle,
    int MemberId,
    string MemberUsername,
    DateTime CreatedAt,
    string Status,
    int? QueuePosition,
    DateTime? ReadyAt,
    DateTime? ExpiresAt);

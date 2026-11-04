using System.ComponentModel.DataAnnotations;
using CircDesk.Api.Domain;

namespace CircDesk.Api.Dtos;

public record RegisterRequest(
    [Required, MinLength(3), MaxLength(100)] string Username,
    [Required, MinLength(8), MaxLength(100)] string Password);

public record LoginRequest(
    [Required] string Username,
    [Required] string Password);

public record AuthResponse(string Token, DateTime ExpiresAt, string Username, MemberRole Role);

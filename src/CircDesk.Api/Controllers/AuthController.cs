using CircDesk.Api.Auth;
using CircDesk.Api.Common;
using CircDesk.Api.Data;
using CircDesk.Api.Domain;
using CircDesk.Api.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace CircDesk.Api.Controllers;

[ApiController]
[Route("api/auth")]
[EnableRateLimiting("auth")]
public class AuthController : ControllerBase
{
    private readonly CircDeskDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;

    public AuthController(CircDeskDbContext db, IPasswordHasher passwordHasher, IJwtTokenService jwtTokenService)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request, CancellationToken ct)
    {
        var username = request.Username.Trim();

        if (await _db.Members.AnyAsync(m => m.Username == username, ct))
        {
            throw new ConflictException($"El usuario '{username}' ya existe.");
        }

        var (hash, salt) = _passwordHasher.Hash(request.Password);

        var member = new Member
        {
            Username = username,
            PasswordHash = hash,
            PasswordSalt = salt,
            Role = MemberRole.Member,
            CreatedAt = DateTime.UtcNow
        };

        _db.Members.Add(member);
        await _db.SaveChangesAsync(ct);

        return Ok(BuildAuthResponse(member));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        var username = request.Username.Trim();
        var member = await _db.Members.FirstOrDefaultAsync(m => m.Username == username, ct);

        if (member is null || !_passwordHasher.Verify(request.Password, member.PasswordHash, member.PasswordSalt))
        {
            throw new InvalidCredentialsException("Usuario o contraseña incorrectos.");
        }

        return Ok(BuildAuthResponse(member));
    }

    private AuthResponse BuildAuthResponse(Member member)
    {
        var (token, expiresAt) = _jwtTokenService.CreateToken(member);
        return new AuthResponse(token, expiresAt, member.Username, member.Role);
    }
}

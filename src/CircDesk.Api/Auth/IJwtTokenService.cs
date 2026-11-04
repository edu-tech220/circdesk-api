using CircDesk.Api.Domain;

namespace CircDesk.Api.Auth;

public interface IJwtTokenService
{
    (string Token, DateTime ExpiresAt) CreateToken(Member member);
}

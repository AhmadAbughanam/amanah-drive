using AmanahDrive.Api.Models;

namespace AmanahDrive.Api.Auth;

public interface ITokenService
{
    string CreateAccessToken(AdminUser user);

    RefreshTokenResult CreateRefreshToken(Guid userId, string? ipAddress);

    string HashRefreshToken(string refreshToken);
}

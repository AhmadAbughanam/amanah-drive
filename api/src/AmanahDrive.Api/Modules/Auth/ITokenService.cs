using AmanahDrive.Api.Modules.Auth.Models;

namespace AmanahDrive.Api.Modules.Auth;

public interface ITokenService
{
    string CreateAccessToken(AdminUser user);

    RefreshTokenResult CreateRefreshToken(Guid userId, string? ipAddress);

    string HashRefreshToken(string refreshToken);
}

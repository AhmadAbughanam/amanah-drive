namespace AmanahDrive.Api.Modules.Auth;

public interface IAuthService
{
    Task<AuthResult> RegisterAsync(string email, string password, string? bootstrapToken, string? ipAddress, CancellationToken cancellationToken);

    Task<AuthResult> LoginAsync(string email, string password, string? ipAddress, CancellationToken cancellationToken);

    Task<AuthResult> RefreshAsync(string? refreshToken, string? ipAddress, CancellationToken cancellationToken);

    Task LogoutAsync(string? refreshToken, string? ipAddress, CancellationToken cancellationToken);
}

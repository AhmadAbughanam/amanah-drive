namespace AmanahDrive.Api.Modules.Auth;

public sealed record AuthResult(
    AuthResultStatus Status,
    string? AccessToken = null,
    string? RefreshToken = null,
    string? Error = null)
{
    public static AuthResult Success(string accessToken, string refreshToken) =>
        new(AuthResultStatus.Success, accessToken, refreshToken);

    public static AuthResult Unauthorized(string error) =>
        new(AuthResultStatus.Unauthorized, Error: error);

    public static AuthResult Forbidden(string error) =>
        new(AuthResultStatus.Forbidden, Error: error);

    public static AuthResult Conflict(string error) =>
        new(AuthResultStatus.Conflict, Error: error);

    public static AuthResult Locked(string error) =>
        new(AuthResultStatus.Locked, Error: error);
}

public enum AuthResultStatus
{
    Success,
    Unauthorized,
    Forbidden,
    Conflict,
    Locked
}

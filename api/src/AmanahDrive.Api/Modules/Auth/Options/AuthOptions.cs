using System.ComponentModel.DataAnnotations;

namespace AmanahDrive.Api.Modules.Auth.Options;

public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    [Required]
    public string JwtIssuer { get; init; } = "AmanahDrive";

    [Required]
    public string JwtAudience { get; init; } = "AmanahDrive";

    [Required]
    public string JwtSigningKey { get; init; } = string.Empty;

    public string? BootstrapToken { get; init; }

    public string RefreshCookieName { get; init; } = "__Host-amanah-refresh";

    public int AccessTokenMinutes { get; init; } = 15;

    public int RefreshTokenDays { get; init; } = 30;

    public bool SecureCookies { get; init; } = true;

    public int LockoutFailedAttempts { get; init; } = 5;

    public int LockoutMinutes { get; init; } = 15;

    public int LoginRateLimitPermitLimit { get; init; } = 20;

    public int LoginRateLimitWindowMinutes { get; init; } = 1;
}

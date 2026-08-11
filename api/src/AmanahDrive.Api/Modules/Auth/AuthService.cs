using System.Security.Cryptography;
using System.Text;
using AmanahDrive.Api.Modules.Auth.Models;
using AmanahDrive.Api.Modules.Auth.Options;
using AmanahDrive.Api.Shared.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AmanahDrive.Api.Modules.Auth;

public sealed class AuthService(
    AmanahDriveDbContext dbContext,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    IOptions<AuthOptions> options,
    ILogger<AuthService> logger) : IAuthService
{
    private readonly AuthOptions _options = options.Value;

    public async Task<AuthResult> RegisterAsync(string email, string password, string? bootstrapToken, string? ipAddress, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.BootstrapToken) ||
            !IsValidBootstrapToken(_options.BootstrapToken, bootstrapToken))
        {
            return AuthResult.Forbidden("Bootstrap registration is disabled or token is invalid.");
        }

        if (await dbContext.AdminUsers.AnyAsync(cancellationToken))
        {
            return AuthResult.Conflict("The administrator account already exists.");
        }

        var normalizedEmail = NormalizeEmail(email);
        var user = new AdminUser
        {
            Id = Guid.NewGuid(),
            Email = email.Trim(),
            NormalizedEmail = normalizedEmail,
            PasswordHash = await passwordHasher.HashAsync(password, cancellationToken),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await dbContext.AdminUsers.AddAsync(user, cancellationToken);
        var refreshToken = tokenService.CreateRefreshToken(user.Id, ipAddress);
        await dbContext.RefreshTokens.AddAsync(refreshToken.Entity, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Admin user {UserId} bootstrapped", user.Id);

        return AuthResult.Success(tokenService.CreateAccessToken(user), refreshToken.PlainTextToken);
    }

    public async Task<AuthResult> LoginAsync(string email, string password, string? ipAddress, CancellationToken cancellationToken)
    {
        var user = await dbContext.AdminUsers
            .SingleOrDefaultAsync(user => user.NormalizedEmail == NormalizeEmail(email), cancellationToken);

        if (user is null)
        {
            return AuthResult.Unauthorized("Invalid credentials.");
        }

        if (user.LockoutEndsAt is not null && user.LockoutEndsAt > DateTimeOffset.UtcNow)
        {
            return AuthResult.Locked("Account is temporarily locked.");
        }

        if (!await passwordHasher.VerifyAsync(password, user.PasswordHash, cancellationToken))
        {
            user.FailedLoginAttempts += 1;
            user.UpdatedAt = DateTimeOffset.UtcNow;

            if (user.FailedLoginAttempts >= _options.LockoutFailedAttempts)
            {
                user.LockoutEndsAt = DateTimeOffset.UtcNow.AddMinutes(_options.LockoutMinutes);
                logger.LogWarning("Admin user {UserId} locked after failed login attempts", user.Id);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return user.LockoutEndsAt is null
                ? AuthResult.Unauthorized("Invalid credentials.")
                : AuthResult.Locked("Account is temporarily locked.");
        }

        user.FailedLoginAttempts = 0;
        user.LockoutEndsAt = null;
        user.UpdatedAt = DateTimeOffset.UtcNow;

        var refreshToken = tokenService.CreateRefreshToken(user.Id, ipAddress);
        await dbContext.RefreshTokens.AddAsync(refreshToken.Entity, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return AuthResult.Success(tokenService.CreateAccessToken(user), refreshToken.PlainTextToken);
    }

    public async Task<AuthResult> RefreshAsync(string? refreshToken, string? ipAddress, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return AuthResult.Unauthorized("Refresh token is missing.");
        }

        var tokenHash = tokenService.HashRefreshToken(refreshToken);
        var existingToken = await dbContext.RefreshTokens
            .Include(token => token.User)
            .SingleOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);

        if (existingToken is null)
        {
            return AuthResult.Unauthorized("Refresh token is invalid.");
        }

        if (!existingToken.IsActive)
        {
            await RevokeUserRefreshTokensAsync(existingToken.UserId, ipAddress, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            return AuthResult.Unauthorized("Refresh token is invalid.");
        }

        var replacement = tokenService.CreateRefreshToken(existingToken.UserId, ipAddress);
        existingToken.RotatedAt = DateTimeOffset.UtcNow;
        existingToken.RevokedAt = DateTimeOffset.UtcNow;
        existingToken.RevokedByIp = ipAddress;
        existingToken.ReplacedByTokenHash = replacement.Entity.TokenHash;

        await dbContext.RefreshTokens.AddAsync(replacement.Entity, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return AuthResult.Success(tokenService.CreateAccessToken(existingToken.User), replacement.PlainTextToken);
    }

    public async Task LogoutAsync(string? refreshToken, string? ipAddress, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return;
        }

        var tokenHash = tokenService.HashRefreshToken(refreshToken);
        var existingToken = await dbContext.RefreshTokens
            .SingleOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);

        if (existingToken is null || !existingToken.IsActive)
        {
            return;
        }

        existingToken.RevokedAt = DateTimeOffset.UtcNow;
        existingToken.RevokedByIp = ipAddress;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task RevokeUserRefreshTokensAsync(Guid userId, string? ipAddress, CancellationToken cancellationToken)
    {
        var activeTokens = await dbContext.RefreshTokens
            .Where(token => token.UserId == userId && token.RevokedAt == null && token.ExpiresAt > DateTimeOffset.UtcNow)
            .ToListAsync(cancellationToken);

        foreach (var token in activeTokens)
        {
            token.RevokedAt = DateTimeOffset.UtcNow;
            token.RevokedByIp = ipAddress;
        }
    }

    private static string NormalizeEmail(string email) => email.Trim().ToUpperInvariant();

    private static bool IsValidBootstrapToken(string expectedToken, string? providedToken)
    {
        if (providedToken is null)
        {
            return false;
        }

        var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(expectedToken));
        var providedHash = SHA256.HashData(Encoding.UTF8.GetBytes(providedToken));

        return CryptographicOperations.FixedTimeEquals(expectedHash, providedHash);
    }
}

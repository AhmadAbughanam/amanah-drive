using AmanahDrive.Api.Models;

namespace AmanahDrive.Api.Auth;

public sealed record RefreshTokenResult(string PlainTextToken, RefreshToken Entity);

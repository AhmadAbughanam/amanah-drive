using AmanahDrive.Api.Modules.Auth.Models;

namespace AmanahDrive.Api.Modules.Auth;

public sealed record RefreshTokenResult(string PlainTextToken, RefreshToken Entity);

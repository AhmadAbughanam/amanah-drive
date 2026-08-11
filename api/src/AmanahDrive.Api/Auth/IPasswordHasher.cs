namespace AmanahDrive.Api.Auth;

public interface IPasswordHasher
{
    Task<string> HashAsync(string password, CancellationToken cancellationToken);

    Task<bool> VerifyAsync(string password, string passwordHash, CancellationToken cancellationToken);
}

using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;

namespace AmanahDrive.Api.Modules.Auth;

public sealed class Argon2idPasswordHasher : IPasswordHasher
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int DegreeOfParallelism = 2;
    private const int Iterations = 3;
    private const int MemorySizeKb = 64 * 1024;

    public async Task<string> HashAsync(string password, CancellationToken cancellationToken)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = await HashPasswordAsync(password, salt, cancellationToken);

        return string.Join(
            "$",
            "argon2id",
            "v=19",
            $"m={MemorySizeKb},t={Iterations},p={DegreeOfParallelism}",
            Convert.ToBase64String(salt),
            Convert.ToBase64String(hash));
    }

    public async Task<bool> VerifyAsync(string password, string passwordHash, CancellationToken cancellationToken)
    {
        var parts = passwordHash.Split('$');
        if (parts.Length != 5 || parts[0] != "argon2id")
        {
            return false;
        }

        var salt = Convert.FromBase64String(parts[3]);
        var expectedHash = Convert.FromBase64String(parts[4]);
        var actualHash = await HashPasswordAsync(password, salt, cancellationToken);

        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }

    private static async Task<byte[]> HashPasswordAsync(string password, byte[] salt, CancellationToken cancellationToken)
    {
        var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            DegreeOfParallelism = DegreeOfParallelism,
            Iterations = Iterations,
            MemorySize = MemorySizeKb
        };

        return await argon2.GetBytesAsync(HashSize);
    }
}

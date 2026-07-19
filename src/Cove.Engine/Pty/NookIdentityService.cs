using System.Security.Cryptography;
using System.Text;

namespace Cove.Engine.Pty;

internal readonly record struct NookIdentity(string NookId, string Token);

internal sealed class NookIdentityService
{
    public NookIdentity Allocate() => Issue("nook-" + Guid.NewGuid().ToString("N"));

    public NookIdentity Issue(string nookId) => new(
        nookId,
        Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));

    public NookAuthResult Authenticate(string storedToken, string? candidateToken)
    {
        if (string.IsNullOrEmpty(storedToken) || string.IsNullOrEmpty(candidateToken))
            return NookAuthResult.Rejected;

        var storedBytes = Encoding.ASCII.GetBytes(storedToken);
        var candidateBytes = Encoding.ASCII.GetBytes(candidateToken);
        return CryptographicOperations.FixedTimeEquals(storedBytes, candidateBytes)
            ? NookAuthResult.Bound
            : NookAuthResult.Rejected;
    }
}

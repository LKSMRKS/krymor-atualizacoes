using System.Security.Cryptography;
using System.Text;

namespace CinzasDaMare.Server.Persistence;

public static class TokenHasher
{
    /// <summary>Token aleatório de 256 bits, em Base64 seguro para URL.</summary>
    public static string NewToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    public static string Hash(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
}

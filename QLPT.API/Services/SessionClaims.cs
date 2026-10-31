using System.Security.Cryptography;
using System.Text;

namespace QLPT.API.Services;

public static class SessionClaims
{
    public static string PasswordStamp(string hash) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(hash)));
}

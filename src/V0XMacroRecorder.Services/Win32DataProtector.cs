using System.Security.Cryptography;
using System.Text;
using V0XMacroRecorder.Core.Abstractions;

namespace V0XMacroRecorder.Services;

/// <summary>DPAPI (<see cref="ProtectedData"/>), portée utilisateur courant : le secret ne se déchiffre que pour ce compte Windows sur cette machine.</summary>
public sealed class Win32DataProtector : IDataProtector
{
    public string Protect(string plainText)
    {
        var bytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(plainText), null, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(bytes);
    }

    public string Unprotect(string protectedText)
    {
        var bytes = ProtectedData.Unprotect(Convert.FromBase64String(protectedText), null, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(bytes);
    }
}

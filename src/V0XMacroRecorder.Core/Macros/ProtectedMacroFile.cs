using System.Security.Cryptography;
using System.Text;

namespace V0XMacroRecorder.Core.Macros;

/// <summary>
/// Enveloppe optionnelle par mot de passe pour le contenu (JSON) d'un fichier <c>.v0xmacro</c> (étape 8) :
/// PBKDF2 (SHA-256, 200 000 itérations) dérive une clé AES-256 du mot de passe avec un sel aléatoire par fichier ;
/// AES-GCM chiffre le JSON et produit une étiquette d'authentification qui sert aussi de signature d'intégrité —
/// toute modification du fichier (mot de passe correct ou non) fait échouer <see cref="Decrypt"/> plutôt que de
/// silencieusement charger un contenu altéré. Format textuel (pas binaire) pour rester un fichier lisible comme
/// le format non protégé : <c>Prefix + base64(sel(16) + nonce(12) + étiquette(16) + texte chiffré)</c>.
/// </summary>
public static class ProtectedMacroFile
{
    private const string Prefix = "V0XPMF1:";
    private const int SaltSize = 16;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int Iterations = 200_000;
    private const int KeySizeBytes = 32; // AES-256

    public static bool IsProtected(string fileContent) => fileContent.StartsWith(Prefix, StringComparison.Ordinal);

    public static string Encrypt(string json, string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var key = DeriveKey(password, salt);

        var plainBytes = Encoding.UTF8.GetBytes(json);
        var cipherBytes = new byte[plainBytes.Length];
        var tag = new byte[TagSize];

        using (var aes = new AesGcm(key, TagSize))
        {
            aes.Encrypt(nonce, plainBytes, cipherBytes, tag);
        }

        var payload = new byte[SaltSize + NonceSize + TagSize + cipherBytes.Length];
        Buffer.BlockCopy(salt, 0, payload, 0, SaltSize);
        Buffer.BlockCopy(nonce, 0, payload, SaltSize, NonceSize);
        Buffer.BlockCopy(tag, 0, payload, SaltSize + NonceSize, TagSize);
        Buffer.BlockCopy(cipherBytes, 0, payload, SaltSize + NonceSize + TagSize, cipherBytes.Length);

        return Prefix + Convert.ToBase64String(payload);
    }

    /// <summary>Lève <see cref="MacroFormatException"/> si le mot de passe est incorrect ou le fichier corrompu/modifié.</summary>
    public static string Decrypt(string fileContent, string password)
    {
        if (!IsProtected(fileContent))
        {
            throw new MacroFormatException("Ce fichier n'est pas protégé par un mot de passe.");
        }

        ArgumentException.ThrowIfNullOrEmpty(password);

        byte[] payload;
        try
        {
            payload = Convert.FromBase64String(fileContent[Prefix.Length..]);
        }
        catch (FormatException ex)
        {
            throw new MacroFormatException("Ce fichier protégé est corrompu.", ex);
        }

        if (payload.Length < SaltSize + NonceSize + TagSize)
        {
            throw new MacroFormatException("Ce fichier protégé est corrompu.");
        }

        var salt = payload[..SaltSize];
        var nonce = payload[SaltSize..(SaltSize + NonceSize)];
        var tag = payload[(SaltSize + NonceSize)..(SaltSize + NonceSize + TagSize)];
        var cipherBytes = payload[(SaltSize + NonceSize + TagSize)..];
        var key = DeriveKey(password, salt);
        var plainBytes = new byte[cipherBytes.Length];

        try
        {
            using var aes = new AesGcm(key, TagSize);
            aes.Decrypt(nonce, cipherBytes, tag, plainBytes);
        }
        catch (CryptographicException ex)
        {
            // Message volontairement générique : ne pas distinguer "mot de passe faux" de "fichier modifié" évite
            // de révéler à un attaquant lequel des deux s'est produit.
            throw new MacroFormatException("Mot de passe incorrect, ou ce fichier a été modifié depuis qu'il a été protégé.", ex);
        }

        return Encoding.UTF8.GetString(plainBytes);
    }

    private static byte[] DeriveKey(string password, byte[] salt) =>
        Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, Iterations, HashAlgorithmName.SHA256, KeySizeBytes);
}

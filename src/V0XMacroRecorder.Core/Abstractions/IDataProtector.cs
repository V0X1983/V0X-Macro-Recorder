namespace V0XMacroRecorder.Core.Abstractions;

/// <summary>
/// Chiffre une valeur pour l'utilisateur Windows courant sur cette machine (DPAPI, <c>ProtectedData</c>), pour la
/// commande « Saisie protégée » (étape 8) : jamais en clair dans le fichier macro. Portée volontairement étroite
/// (utilisateur + machine courants) : une macro contenant un secret protégé ne fonctionnera pas si on la copie sur
/// un autre ordinateur ou sous un autre compte Windows — documenté, pas un bug.
/// </summary>
public interface IDataProtector
{
    string Protect(string plainText);

    /// <summary>Lève si <paramref name="protectedText"/> n'a pas été chiffré par ce même utilisateur/cette même machine.</summary>
    string Unprotect(string protectedText);
}

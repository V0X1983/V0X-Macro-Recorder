namespace V0XMacroRecorder.Core.Abstractions;

/// <summary>
/// Détecte si l'entrée (souris/clavier) ne peut pas atteindre le bureau normal en ce moment (étape 8) : écran de
/// verrouillage, UAC Secure Desktop (invite d'élévation), changement d'utilisateur rapide. Dans ces cas,
/// <c>SendInput</c> n'échoue pas franchement mais l'entrée n'arrive jamais à la bonne fenêtre — mieux vaut
/// détecter et avertir avant d'injecter à l'aveugle.
/// </summary>
public interface ISessionLockService
{
    bool IsInputSessionLocked();
}

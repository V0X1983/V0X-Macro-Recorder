namespace V0XMacroRecorder.Core.Abstractions;

/// <summary>
/// Détecte si l'élément actuellement focalisé est un champ sensible (mot de passe), pour que l'enregistreur
/// n'y capture jamais de frappes quand l'option « Ne pas enregistrer les champs mot de passe » est active.
/// </summary>
public interface ISecureInputGuard
{
    bool IsFocusedElementSensitive();
}

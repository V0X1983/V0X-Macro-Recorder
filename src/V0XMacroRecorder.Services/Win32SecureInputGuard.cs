using System.Runtime.InteropServices;
using System.Windows.Automation;
using V0XMacroRecorder.Core.Abstractions;

namespace V0XMacroRecorder.Services;

/// <summary>
/// Détection au mieux (best effort) d'un champ mot de passe via UI Automation. N'est pas une garantie de sécurité
/// absolue (une application qui n'expose pas correctement <see cref="AutomationElement.IsPasswordProperty"/> ne
/// sera pas détectée) : c'est une protection supplémentaire, pas la seule ligne de défense pour les secrets
/// (voir la commande « Saisie protégée » prévue à l'étape 8).
/// </summary>
public sealed class Win32SecureInputGuard : ISecureInputGuard
{
    public bool IsFocusedElementSensitive()
    {
        try
        {
            var element = AutomationElement.FocusedElement;
            return element is not null && element.GetCurrentPropertyValue(AutomationElement.IsPasswordProperty) is true;
        }
        catch (Exception ex) when (ex is ElementNotAvailableException or InvalidOperationException or COMException)
        {
            // Élément disparu ou application qui ne répond pas à la requête UI Automation : on n'empêche pas
            // l'enregistrement pour autant, cette protection est complémentaire (voir résumé ci-dessus).
            return false;
        }
    }
}

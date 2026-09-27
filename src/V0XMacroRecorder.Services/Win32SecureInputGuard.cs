using System.Runtime.InteropServices;
using Interop.UIAutomationClient;
using V0XMacroRecorder.Core.Abstractions;

namespace V0XMacroRecorder.Services;

/// <summary>
/// Détection au mieux (best effort) d'un champ mot de passe via UI Automation (interop COM natif
/// <see cref="Interop.UIAutomationClient"/> — évite de dépendre de l'assembly WPF <c>System.Windows.Automation</c>,
/// pour que ce projet reste utilisable depuis une UI non-WPF). N'est pas une garantie de sécurité absolue (une
/// application qui n'expose pas correctement la propriété IsPassword ne sera pas détectée) : c'est une protection
/// supplémentaire, pas la seule ligne de défense pour les secrets (voir la commande « Saisie protégée » prévue à
/// l'étape 8).
/// </summary>
public sealed class Win32SecureInputGuard : ISecureInputGuard
{
    private readonly IUIAutomation _automation = new CUIAutomationClass();

    public bool IsFocusedElementSensitive()
    {
        try
        {
            var element = _automation.GetFocusedElement();
            if (element is null)
            {
                return false;
            }

            var value = element.GetCurrentPropertyValue(UIA_PropertyIds.UIA_IsPasswordPropertyId);
            return value is bool isPassword && isPassword;
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException)
        {
            // Élément disparu ou application qui ne répond pas à la requête UI Automation : on n'empêche pas
            // l'enregistrement pour autant, cette protection est complémentaire (voir résumé ci-dessus).
            return false;
        }
    }
}

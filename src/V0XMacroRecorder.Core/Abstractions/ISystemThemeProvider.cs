namespace V0XMacroRecorder.Core.Abstractions;

/// <summary>Thème clair/sombre actuellement choisi dans les paramètres Windows (mode « Système », étape 7).</summary>
public interface ISystemThemeProvider
{
    bool IsDarkThemeActive();

    /// <summary>Notifié quand l'utilisateur change de thème dans les paramètres Windows pendant que l'application tourne.</summary>
    event EventHandler? Changed;
}

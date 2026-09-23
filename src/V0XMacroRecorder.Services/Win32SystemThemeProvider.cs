using Microsoft.Win32;
using V0XMacroRecorder.Core.Abstractions;

namespace V0XMacroRecorder.Services;

/// <summary>
/// Lit la préférence clair/sombre des paramètres Windows (clé <c>AppsUseLightTheme</c>, valeur "app", pas "système" :
/// c'est celle que suivent la plupart des applications, y compris l'Explorateur). Absente/illisible → clair par
/// défaut (comportement Windows si la clé n'existe pas, ex. éditions Server).
/// </summary>
public sealed class Win32SystemThemeProvider : IDisposable, ISystemThemeProvider
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string ValueName = "AppsUseLightTheme";

    public Win32SystemThemeProvider() => SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;

    public event EventHandler? Changed;

    public bool IsDarkThemeActive()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
            return key?.GetValue(ValueName) is int value && value == 0;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            return false;
        }
    }

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category == UserPreferenceCategory.General)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Dispose() => SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
}

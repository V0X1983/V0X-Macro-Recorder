using System.Windows;
using V0XMacroRecorder.Core.Models;

namespace V0XMacroRecorder.App.Helpers;

/// <summary>Bascule le thème clair/sombre en remplaçant le ResourceDictionary "Themes/*" chargé.</summary>
public static class ThemeManager
{
    public static void ApplyTheme(string theme)
    {
        var app = Application.Current;
        var uri = new Uri(
            theme == AppSettings.LightTheme ? "Resources/Themes/Light.xaml" : "Resources/Themes/Dark.xaml",
            UriKind.Relative);

        var newDictionary = new ResourceDictionary { Source = uri };
        var dictionaries = app.Resources.MergedDictionaries;

        var existingIndex = -1;
        for (var i = 0; i < dictionaries.Count; i++)
        {
            if (dictionaries[i].Source?.OriginalString.Contains("Resources/Themes/") == true)
            {
                existingIndex = i;
                break;
            }
        }

        if (existingIndex >= 0)
        {
            dictionaries[existingIndex] = newDictionary;
        }
        else
        {
            dictionaries.Insert(0, newDictionary);
        }
    }
}

using Microsoft.UI.Xaml;
using V0XMacroRecorder.App.Infrastructure;
using V0XMacroRecorder.Core.Models;

namespace V0XMacroRecorder.App.Helpers;

/// <summary>
/// Bascule le thème clair/sombre. WinUI 3 n'a pas d'équivalent de l'échange de <c>ResourceDictionary</c> WPF : le
/// thème se pilote via <see cref="FrameworkElement.RequestedTheme"/> sur la racine visuelle de chaque fenêtre
/// (pas de thème "global" côté <see cref="Application"/>). <see cref="Initialize"/> est appelé une fois par
/// <c>App.xaml.cs</c> ; les fenêtres ouvertes ultérieurement (Phase 3) devront s'enregistrer via <see cref="Register"/>
/// pour rester synchronisées avec un changement de thème pendant qu'elles sont ouvertes.
/// </summary>
public static class ThemeManager
{
    private static IAppWindowProvider? _appWindow;
    private static readonly List<WeakReference<FrameworkElement>> ExtraRoots = [];

    public static void Initialize(IAppWindowProvider appWindow) => _appWindow = appWindow;

    /// <summary>À appeler par toute fenêtre secondaire (Phase 3) juste après sa création, pour suivre les changements de thème.</summary>
    public static void Register(FrameworkElement root) => ExtraRoots.Add(new WeakReference<FrameworkElement>(root));

    public static void ApplyTheme(string theme)
    {
        var elementTheme = theme == AppSettings.LightTheme ? ElementTheme.Light
            : theme == AppSettings.DarkTheme ? ElementTheme.Dark
            : ElementTheme.Default;

        if (_appWindow?.MainWindow.Content is FrameworkElement mainRoot)
        {
            mainRoot.RequestedTheme = elementTheme;
        }

        for (var i = ExtraRoots.Count - 1; i >= 0; i--)
        {
            if (ExtraRoots[i].TryGetTarget(out var root))
            {
                root.RequestedTheme = elementTheme;
            }
            else
            {
                ExtraRoots.RemoveAt(i); // Fenêtre fermée/récupérée par le GC : nettoyage au passage.
            }
        }
    }
}

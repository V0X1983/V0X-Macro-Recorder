using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Windows.Graphics;
using WinRT.Interop;

namespace V0XMacroRecorder.App.Helpers;

/// <summary>
/// WinUI 3 n'a pas d'équivalent à <c>Window.Width</c>/<c>Height</c>/<c>SizeToContent</c> de WPF : sans appel explicite
/// à <see cref="Microsoft.UI.Windowing.AppWindow.Resize"/>, une fenêtre secondaire (Paramètres, éditeur de commande…)
/// s'ouvre à la taille par défaut de la plateforme (bien plus grande qu'une boîte de dialogue) — bug réel signalé par
/// l'utilisateur (« les fenêtres semblent trop grandes »). <see cref="Set"/> reprend approximativement les dimensions
/// `Width`/`Height`/`SizeToContent="Height"` de la fenêtre WPF équivalente.
///
/// Utilise <c>GetDpiForWindow</c> (le HWND existe déjà juste après <c>InitializeComponent()</c>, avant tout
/// affichage) plutôt que <c>Content.XamlRoot.RasterizationScale</c> : ce dernier n'est pas garanti renseigné avant
/// que la fenêtre soit réellement affichée, ce qui forçait un redimensionnement différé (sur <c>Loaded</c>) — visible
/// à l'écran comme un « saut » de taille au premier affichage (second bug réel signalé par l'utilisateur). Avec
/// GetDpiForWindow, la fenêtre est redimensionnée avant même le premier <c>Activate()</c>, donc jamais montrée à la
/// mauvaise taille. Contrairement à WPF, aucune tentative de dimensionnement automatique précis au contenu n'est
/// faite ici (mesurer le contenu réel avant l'affichage est nettement plus complexe en WinUI 3) — simplification
/// assumée, les fenêtres restent redimensionnables.
/// </summary>
internal static class WindowSizing
{
    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint hWnd);

    public static void Set(Window window, double widthDip, double heightDip)
    {
        var hwnd = WindowNative.GetWindowHandle(window);
        var scale = GetDpiForWindow(hwnd) / 96.0;
        window.AppWindow.Resize(new SizeInt32((int)(widthDip * scale), (int)(heightDip * scale)));
    }
}

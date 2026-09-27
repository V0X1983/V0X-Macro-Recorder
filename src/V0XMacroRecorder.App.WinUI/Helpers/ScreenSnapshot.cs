using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml.Media.Imaging;
using V0XMacroRecorder.Core.Playback;

namespace V0XMacroRecorder.App.Helpers;

/// <summary>
/// Convertit une <see cref="CapturedBitmap"/> (capture GDI brute) en <see cref="WriteableBitmap"/> affichable.
/// Utilisé par les fenêtres de capture plein écran (pipette de couleur, sélection de région pour la recherche
/// d'image) : contrairement à WPF (fenêtre réellement transparente via <c>AllowsTransparency</c>, le vrai bureau
/// apparaît derrière), une <see cref="Microsoft.UI.Xaml.Window"/> WinUI 3 compose sur une surface opaque — sans rien
/// affiché, elle rend simplement noire. On affiche donc une capture figée du bureau en fond de fenêtre à la place.
/// </summary>
internal static class ScreenSnapshot
{
    public static WriteableBitmap ToWriteableBitmap(CapturedBitmap capture)
    {
        var bitmap = new WriteableBitmap(capture.Width, capture.Height);
        var pixels = capture.PixelsBgra32;
        var opaque = new byte[pixels.Length];
        Array.Copy(pixels, opaque, pixels.Length);
        for (var i = 3; i < opaque.Length; i += 4)
        {
            opaque[i] = 0xFF; // GetDIBits (BI_RGB) ne renseigne pas le canal alpha : forcé opaque, une capture d'écran n'en a pas besoin.
        }

        using (var stream = bitmap.PixelBuffer.AsStream())
        {
            stream.Write(opaque, 0, opaque.Length);
        }

        bitmap.Invalidate();
        return bitmap;
    }
}

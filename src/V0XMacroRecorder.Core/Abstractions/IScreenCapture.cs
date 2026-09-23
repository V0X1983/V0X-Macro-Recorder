using V0XMacroRecorder.Core.Macros;
using V0XMacroRecorder.Core.Playback;

namespace V0XMacroRecorder.Core.Abstractions;

/// <summary>Capture d'écran brute (recherche d'image, outil de capture de région à l'édition).</summary>
public interface IScreenCapture
{
    /// <summary>Pixels BGRA32 de la région donnée (coordonnées écran physiques, multi-écran/DPI géré côté implémentation).</summary>
    CapturedBitmap Capture(RectRegion region);

    RectRegion VirtualScreenBounds { get; }
}

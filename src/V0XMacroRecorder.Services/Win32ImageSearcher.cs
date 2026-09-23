using V0XMacroRecorder.Core.Abstractions;
using V0XMacroRecorder.Core.Macros;
using V0XMacroRecorder.Core.Playback;

namespace V0XMacroRecorder.Services;

/// <summary>Compose capture d'écran + décodage PNG + comparaison pixel à pixel (<see cref="ImageSearcher"/>, pur Core).</summary>
public sealed class Win32ImageSearcher(IScreenCapture screenCapture, IImageCodec codec) : IImageSearcher
{
    public (int X, int Y)? Find(byte[] templatePngBytes, RectRegion? searchRegion, int tolerancePercent)
    {
        var region = searchRegion ?? screenCapture.VirtualScreenBounds;
        var haystack = screenCapture.Capture(region);
        var needle = codec.DecodePng(templatePngBytes);
        var found = ImageSearcher.Find(haystack, needle, tolerancePercent);
        return found is { } offset ? (region.X + offset.X, region.Y + offset.Y) : null;
    }
}

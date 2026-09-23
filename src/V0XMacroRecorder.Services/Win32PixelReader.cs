using V0XMacroRecorder.Core.Abstractions;
using V0XMacroRecorder.Services.Native;

namespace V0XMacroRecorder.Services;

/// <summary>Lit un pixel via le DC de l'écran entier (GetDC(NULL) + GetPixel), coordonnées physiques (app PerMonitorV2).</summary>
public sealed class Win32PixelReader : IPixelReader
{
    public (byte R, byte G, byte B)? GetPixelColor(int screenX, int screenY)
    {
        var dc = NativeMethods.GetDC(IntPtr.Zero);
        if (dc == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            var colorRef = NativeMethods.GetPixel(dc, screenX, screenY);
            if (colorRef == NativeMethods.CLR_INVALID)
            {
                return null;
            }

            // COLORREF est 0x00BBGGRR.
            var r = (byte)(colorRef & 0xFF);
            var g = (byte)((colorRef >> 8) & 0xFF);
            var b = (byte)((colorRef >> 16) & 0xFF);
            return (r, g, b);
        }
        finally
        {
            NativeMethods.ReleaseDC(IntPtr.Zero, dc);
        }
    }
}

using System.Runtime.InteropServices;
using V0XMacroRecorder.Core.Abstractions;
using V0XMacroRecorder.Core.Macros;
using V0XMacroRecorder.Core.Playback;
using V0XMacroRecorder.Services.Native;

namespace V0XMacroRecorder.Services;

/// <summary>Capture GDI (BitBlt + GetDIBits) en BGRA32, coordonnées physiques (app PerMonitorV2, comme le reste du moteur).</summary>
public sealed class Win32ScreenCapture : IScreenCapture
{
    public RectRegion VirtualScreenBounds => new()
    {
        X = NativeMethods.GetSystemMetrics(NativeMethods.SM_XVIRTUALSCREEN),
        Y = NativeMethods.GetSystemMetrics(NativeMethods.SM_YVIRTUALSCREEN),
        Width = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXVIRTUALSCREEN),
        Height = NativeMethods.GetSystemMetrics(NativeMethods.SM_CYVIRTUALSCREEN),
    };

    public CapturedBitmap Capture(RectRegion region)
    {
        var width = Math.Max(1, region.Width);
        var height = Math.Max(1, region.Height);

        var screenDc = NativeMethods.GetDC(IntPtr.Zero);
        var memDc = NativeMethods.CreateCompatibleDC(screenDc);
        var bitmap = NativeMethods.CreateCompatibleBitmap(screenDc, width, height);
        var oldBitmap = NativeMethods.SelectObject(memDc, bitmap);
        try
        {
            NativeMethods.BitBlt(memDc, 0, 0, width, height, screenDc, region.X, region.Y, NativeMethods.SRCCOPY);

            var info = new NativeMethods.BITMAPINFO
            {
                bmiHeader = new NativeMethods.BITMAPINFOHEADER
                {
                    biSize = (uint)Marshal.SizeOf<NativeMethods.BITMAPINFOHEADER>(),
                    biWidth = width,
                    biHeight = -height, // négatif = top-down, évite de retourner l'image nous-mêmes.
                    biPlanes = 1,
                    biBitCount = 32,
                    biCompression = NativeMethods.BI_RGB,
                },
            };

            var pixels = new byte[width * height * 4];
            NativeMethods.GetDIBits(memDc, bitmap, 0, (uint)height, pixels, ref info, NativeMethods.DIB_RGB_COLORS);
            return new CapturedBitmap(width, height, pixels);
        }
        finally
        {
            NativeMethods.SelectObject(memDc, oldBitmap);
            NativeMethods.DeleteObject(bitmap);
            NativeMethods.DeleteDC(memDc);
            NativeMethods.ReleaseDC(IntPtr.Zero, screenDc);
        }
    }
}

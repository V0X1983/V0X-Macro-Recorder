using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using V0XMacroRecorder.Core.Abstractions;
using V0XMacroRecorder.Core.Playback;

namespace V0XMacroRecorder.Services;

/// <summary>PNG ↔ BGRA32 via System.Drawing.Common (GDI+), indépendant de toute UI applicative (WPF/WinUI).</summary>
public sealed class Win32ImageCodec : IImageCodec
{
    public CapturedBitmap DecodePng(byte[] pngBytes)
    {
        using var stream = new MemoryStream(pngBytes);
        using var loaded = new Bitmap(stream);
        using var bitmap = loaded.PixelFormat == PixelFormat.Format32bppArgb
            ? loaded
            : loaded.Clone(new Rectangle(0, 0, loaded.Width, loaded.Height), PixelFormat.Format32bppArgb);

        var width = bitmap.Width;
        var height = bitmap.Height;
        var pixels = new byte[width * height * 4];

        var data = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            for (var row = 0; row < height; row++)
            {
                System.Runtime.InteropServices.Marshal.Copy(data.Scan0 + row * data.Stride, pixels, row * width * 4, width * 4);
            }
        }
        finally
        {
            bitmap.UnlockBits(data);
        }

        return new CapturedBitmap(width, height, pixels);
    }

    public byte[] EncodePng(CapturedBitmap bitmap)
    {
        using var target = new Bitmap(bitmap.Width, bitmap.Height, PixelFormat.Format32bppArgb);
        var data = target.LockBits(
            new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try
        {
            for (var row = 0; row < bitmap.Height; row++)
            {
                System.Runtime.InteropServices.Marshal.Copy(
                    bitmap.PixelsBgra32, row * bitmap.Width * 4, data.Scan0 + row * data.Stride, bitmap.Width * 4);
            }
        }
        finally
        {
            target.UnlockBits(data);
        }

        using var stream = new MemoryStream();
        target.Save(stream, ImageFormat.Png);
        return stream.ToArray();
    }
}

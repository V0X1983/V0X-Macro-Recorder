using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using V0XMacroRecorder.Core.Abstractions;
using V0XMacroRecorder.Core.Playback;

namespace V0XMacroRecorder.Services;

/// <summary>PNG ↔ BGRA32 via l'imagerie WPF (déjà référencée pour Win32ClipboardService/Win32MessageBoxService).</summary>
public sealed class Win32ImageCodec : IImageCodec
{
    public CapturedBitmap DecodePng(byte[] pngBytes)
    {
        using var stream = new MemoryStream(pngBytes);
        var decoder = new PngBitmapDecoder(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        var frame = new FormatConvertedBitmap(decoder.Frames[0], PixelFormats.Bgra32, null, 0);

        var width = frame.PixelWidth;
        var height = frame.PixelHeight;
        var pixels = new byte[width * height * 4];
        frame.CopyPixels(pixels, width * 4, 0);
        return new CapturedBitmap(width, height, pixels);
    }

    public byte[] EncodePng(CapturedBitmap bitmap)
    {
        var source = BitmapSource.Create(
            bitmap.Width, bitmap.Height, 96, 96, PixelFormats.Bgra32, null,
            bitmap.PixelsBgra32, bitmap.Width * 4);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(source));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }
}

using V0XMacroRecorder.Core.Playback;

namespace V0XMacroRecorder.Core.Abstractions;

/// <summary>Conversion PNG ↔ pixels bruts (le décodage PNG n'est pas portable en Core pur, fait côté Services).</summary>
public interface IImageCodec
{
    CapturedBitmap DecodePng(byte[] pngBytes);

    byte[] EncodePng(CapturedBitmap bitmap);
}

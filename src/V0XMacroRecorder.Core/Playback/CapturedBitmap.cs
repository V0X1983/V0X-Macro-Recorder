namespace V0XMacroRecorder.Core.Playback;

/// <summary>Pixels BGRA32 (4 octets/pixel, stride = Width*4) d'une capture d'écran ou d'un modèle décodé.</summary>
public readonly record struct CapturedBitmap(int Width, int Height, byte[] PixelsBgra32);

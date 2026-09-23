namespace V0XMacroRecorder.Core.Abstractions;

/// <summary>Lit la couleur d'un pixel à l'écran (commande Pixel, condition « Pixel » du contrôle de flux).</summary>
public interface IPixelReader
{
    /// <summary>Couleur RGB au point écran donné, ou null si le point est hors de tout écran.</summary>
    (byte R, byte G, byte B)? GetPixelColor(int screenX, int screenY);
}

namespace V0XMacroRecorder.Core.Playback;

/// <summary>
/// Comparaison de couleur à tolérance (commande Pixel, condition Si, et recherche d'image pixel par pixel) :
/// une seule règle de tolérance partagée dans tout le moteur.
/// </summary>
public static class ColorMatch
{
    /// <summary>Vrai si chaque canal RGB diffère de moins de <paramref name="tolerancePercent"/>% de 255.</summary>
    public static bool Matches((byte R, byte G, byte B) actual, (byte R, byte G, byte B) expected, int tolerancePercent)
    {
        var maxDiff = tolerancePercent / 100.0 * 255;
        return Math.Abs(actual.R - expected.R) <= maxDiff
            && Math.Abs(actual.G - expected.G) <= maxDiff
            && Math.Abs(actual.B - expected.B) <= maxDiff;
    }

    /// <summary>Analyse "#RRGGBB" (insensible à la casse) ; (0,0,0) si le texte est invalide.</summary>
    public static (byte R, byte G, byte B) ParseHex(string? hex)
    {
        if (string.IsNullOrEmpty(hex))
        {
            return (0, 0, 0);
        }

        var span = hex.AsSpan().TrimStart('#');
        if (span.Length != 6
            || !byte.TryParse(span[..2], System.Globalization.NumberStyles.HexNumber, null, out var r)
            || !byte.TryParse(span[2..4], System.Globalization.NumberStyles.HexNumber, null, out var g)
            || !byte.TryParse(span[4..6], System.Globalization.NumberStyles.HexNumber, null, out var b))
        {
            return (0, 0, 0);
        }

        return (r, g, b);
    }

    public static string ToHex((byte R, byte G, byte B) color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";
}

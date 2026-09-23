namespace V0XMacroRecorder.Core.Playback;

/// <summary>
/// Recherche d'une image modèle (<paramref name="needle"/>) dans une capture d'écran (<paramref name="haystack"/>)
/// par comparaison directe pixel à pixel (<c>unsafe</c>/<c>Span</c>, comme demandé par la roadmap plutôt qu'un
/// algorithme plus complexe). Balayage naïf en O(largeur × hauteur × taille du modèle), avec sortie anticipée dès
/// le premier pixel qui ne correspond pas — suffisant pour un modèle de petite taille (icône, bouton…) recherché
/// occasionnellement ; une piste d'optimisation (ex. filtrage par un seul pixel de référence, FFT…) est documentée
/// ici comme amélioration future, volontairement non faite maintenant.
/// </summary>
public static class ImageSearcher
{
    /// <summary>Position (coin supérieur gauche) de la première correspondance dans <paramref name="haystack"/>, ou null.</summary>
    public static unsafe (int X, int Y)? Find(CapturedBitmap haystack, CapturedBitmap needle, int tolerancePercent)
    {
        if (needle.Width <= 0 || needle.Height <= 0 || needle.Width > haystack.Width || needle.Height > haystack.Height)
        {
            return null;
        }

        var maxDiff = tolerancePercent / 100.0 * 255;
        var hStride = haystack.Width * 4;
        var nStride = needle.Width * 4;

        fixed (byte* hBase = haystack.PixelsBgra32)
        fixed (byte* nBase = needle.PixelsBgra32)
        {
            for (var startY = 0; startY <= haystack.Height - needle.Height; startY++)
            {
                for (var startX = 0; startX <= haystack.Width - needle.Width; startX++)
                {
                    if (MatchesAt(hBase, hStride, nBase, nStride, startX, startY, needle.Width, needle.Height, maxDiff))
                    {
                        return (startX, startY);
                    }
                }
            }
        }

        return null;
    }

    private static unsafe bool MatchesAt(byte* hBase, int hStride, byte* nBase, int nStride, int startX, int startY, int width, int height, double maxDiff)
    {
        for (var ny = 0; ny < height; ny++)
        {
            var hRow = hBase + (startY + ny) * hStride + startX * 4;
            var nRow = nBase + ny * nStride;
            for (var nx = 0; nx < width; nx++)
            {
                var hPixel = hRow + nx * 4;
                var nPixel = nRow + nx * 4;
                // BGRA : B=0, G=1, R=2 (alpha ignoré).
                if (Math.Abs(hPixel[0] - nPixel[0]) > maxDiff
                    || Math.Abs(hPixel[1] - nPixel[1]) > maxDiff
                    || Math.Abs(hPixel[2] - nPixel[2]) > maxDiff)
                {
                    return false;
                }
            }
        }

        return true;
    }
}

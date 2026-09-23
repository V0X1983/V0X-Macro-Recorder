using V0XMacroRecorder.Core.Macros;

namespace V0XMacroRecorder.Core.Abstractions;

/// <summary>Orchestration recherche d'image : capture d'écran + décodage du modèle + comparaison (commandes Image et Attente/ImageFound).</summary>
public interface IImageSearcher
{
    /// <summary>Position (coin supérieur gauche, coordonnées écran) de la première correspondance, ou null.</summary>
    (int X, int Y)? Find(byte[] templatePngBytes, RectRegion? searchRegion, int tolerancePercent);
}

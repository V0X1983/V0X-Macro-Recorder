namespace V0XMacroRecorder.Core.Abstractions;

/// <summary>Retrouve une fenêtre à la lecture pour le repère de coordonnées « Fenêtre active » (recherche par titre/classe).</summary>
public interface IWindowFinder
{
    /// <summary>
    /// Cherche une fenêtre visible dont le titre contient <paramref name="titleContains"/> (insensible à la casse)
    /// et/ou dont la classe Win32 vaut exactement <paramref name="className"/>. Au moins un des deux doit être fourni.
    /// Renvoie la plus récemment active des fenêtres correspondantes, ou null si aucune ne correspond.
    /// </summary>
    nint? FindWindow(string? titleContains, string? className);

    /// <summary>Coin supérieur gauche (coordonnées écran) de la fenêtre, ou null si elle a disparu depuis.</summary>
    (int Left, int Top)? GetTopLeft(nint window);

    /// <summary>Amène la fenêtre au premier plan (nécessaire pour que les touches lui parviennent).</summary>
    bool Activate(nint window);

    /// <summary>
    /// Best-effort : vrai si la fenêtre semble appartenir à un processus plus privilégié que le nôtre (UIPI empêcherait
    /// alors nos entrées de l'atteindre), null si indéterminable. Sert uniquement à avertir l'utilisateur.
    /// </summary>
    bool? IsProbablyElevated(nint window);
}

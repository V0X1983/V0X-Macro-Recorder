namespace V0XMacroRecorder.Core.Macros;

public enum MouseAction
{
    Move,
    Click,
    DoubleClick,
    Down,
    Up,
    Wheel,
}

public enum MouseButton
{
    Left,
    Right,
    Middle,
}

/// <summary>Repère dans lequel sont exprimées les coordonnées d'une commande souris.</summary>
public enum CoordinateMode
{
    /// <summary>Coordonnées absolues du bureau virtuel (multi-écran).</summary>
    Screen,

    /// <summary>Relatives au coin supérieur gauche de la fenêtre active.</summary>
    ActiveWindow,

    /// <summary>Décalage par rapport à la position actuelle du curseur.</summary>
    Relative,
}

public enum KeyAction
{
    /// <summary>Appui puis relâchement.</summary>
    Press,
    Down,
    Up,
}

[Flags]
public enum KeyModifiers
{
    None = 0,
    Ctrl = 1,
    Alt = 2,
    Shift = 4,
    Win = 8,
}

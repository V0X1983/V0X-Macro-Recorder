using V0XMacroRecorder.Core.Macros;

namespace V0XMacroRecorder.Core.Abstractions;

/// <summary>
/// Injecte des entrées souris/clavier (bouton LECTURE). Le clavier s'injecte par code de balayage (scan code)
/// pour marcher indépendamment de la disposition (AZERTY, QWERTY…) ; <paramref name="scanCode"/> à 0 signifie
/// « inconnu » (commande créée manuellement) et l'implémentation réelle le déduit alors du code virtuel via la
/// disposition active.
/// </summary>
public interface IInputSimulator
{
    void MoveMouseTo(int screenX, int screenY);

    (int X, int Y) GetCursorPosition();

    void MouseButton(MouseButton button, bool down);

    /// <summary>Crans de molette : positif vers le haut, négatif vers le bas.</summary>
    void MouseWheel(int notches);

    void KeyEvent(int virtualKey, int scanCode, bool isExtended, bool down);

    /// <summary>Saisie Unicode directe (indépendante du clavier physique).</summary>
    void TypeCharacter(char character);
}

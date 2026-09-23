using V0XMacroRecorder.Core.Macros;

namespace V0XMacroRecorder.Core.Recording;

/// <summary>
/// Événements bruts remontés par le hook Win32 (Services). La détection de clic/double-clic/glisser se fait dans
/// <see cref="RecordingSession"/> (testable), pas dans le hook : seuls Move/Down/Up/Wheel existent ici, tels que
/// livrés par WH_MOUSE_LL.
/// </summary>
public enum RawMouseKind
{
    Move,
    Down,
    Up,
    Wheel,
}

public readonly record struct RawMouseEvent(
    RawMouseKind Kind,
    MouseButton Button,
    int ScreenX,
    int ScreenY,
    int WheelDelta,
    long TimestampMs,
    bool Injected);

/// <summary>Un événement clavier brut (WH_KEYBOARD_LL). <see cref="Character"/> n'est renseigné que pour un appui (Down) résolu par la disposition active.</summary>
public readonly record struct RawKeyEvent(
    int VirtualKey,
    int ScanCode,
    bool IsExtended,
    bool IsKeyDown,
    long TimestampMs,
    bool Injected,
    char? Character);

/// <summary>Fenêtre active au moment d'un événement souris, résolue par la couche Win32 (Services).</summary>
public sealed record ActiveWindowInfo(string Title, string ClassName, int Left, int Top);

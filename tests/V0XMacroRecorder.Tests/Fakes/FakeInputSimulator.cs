using V0XMacroRecorder.Core.Abstractions;
using V0XMacroRecorder.Core.Macros;

namespace V0XMacroRecorder.Tests.Fakes;

public abstract record SimulatedEvent;

public sealed record MoveEvent(int X, int Y) : SimulatedEvent;

public sealed record ButtonEvent(MouseButton Button, bool Down) : SimulatedEvent;

public sealed record WheelEvent(int Notches) : SimulatedEvent;

public sealed record KeyEvent(int VirtualKey, int ScanCode, bool Extended, bool Down) : SimulatedEvent;

public sealed record CharEvent(char Character) : SimulatedEvent;

/// <summary>Enregistre chaque appel pour vérification, sans jamais toucher au vrai système.</summary>
public sealed class FakeInputSimulator : IInputSimulator
{
    public List<SimulatedEvent> Events { get; } = [];

    public int CursorX { get; set; }

    public int CursorY { get; set; }

    public void MoveMouseTo(int screenX, int screenY)
    {
        CursorX = screenX;
        CursorY = screenY;
        Events.Add(new MoveEvent(screenX, screenY));
    }

    public (int X, int Y) GetCursorPosition() => (CursorX, CursorY);

    public void MouseButton(MouseButton button, bool down) => Events.Add(new ButtonEvent(button, down));

    public void MouseWheel(int notches) => Events.Add(new WheelEvent(notches));

    public void KeyEvent(int virtualKey, int scanCode, bool isExtended, bool down) =>
        Events.Add(new KeyEvent(virtualKey, scanCode, isExtended, down));

    public void TypeCharacter(char character) => Events.Add(new CharEvent(character));
}

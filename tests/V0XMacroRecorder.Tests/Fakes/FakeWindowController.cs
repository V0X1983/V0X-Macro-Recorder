using V0XMacroRecorder.Core.Abstractions;

namespace V0XMacroRecorder.Tests.Fakes;

public sealed class FakeWindowController : IWindowController
{
    public sealed record Call(string Action, nint Window, int X = 0, int Y = 0, int Width = 0, int Height = 0);

    public List<Call> Calls { get; } = [];

    public bool Minimize(nint window)
    {
        Calls.Add(new Call("Minimize", window));
        return true;
    }

    public bool Maximize(nint window)
    {
        Calls.Add(new Call("Maximize", window));
        return true;
    }

    public bool Restore(nint window)
    {
        Calls.Add(new Call("Restore", window));
        return true;
    }

    public bool Close(nint window)
    {
        Calls.Add(new Call("Close", window));
        return true;
    }

    public bool MoveResize(nint window, int x, int y, int width, int height)
    {
        Calls.Add(new Call("MoveResize", window, x, y, width, height));
        return true;
    }
}

namespace V0XMacroRecorder.Core.Abstractions;

/// <summary>
/// Actions sur une fenêtre déjà résolue (commande Fenêtre). L'activation seule reste sur
/// <see cref="IWindowFinder.Activate"/>, déjà utilisée par le repère « Fenêtre active ».
/// </summary>
public interface IWindowController
{
    bool Minimize(nint window);

    bool Maximize(nint window);

    bool Restore(nint window);

    bool Close(nint window);

    bool MoveResize(nint window, int x, int y, int width, int height);
}

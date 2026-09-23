using V0XMacroRecorder.Core.Abstractions;

namespace V0XMacroRecorder.Tests.Fakes;

/// <summary>Fenêtres connues à l'avance par le test ; <see cref="Windows"/> peut être modifié en cours de lecture pour simuler une fenêtre qui apparaît après un délai (WaitAndRetry).</summary>
public sealed class FakeWindowFinder : IWindowFinder
{
    public Dictionary<(string? Title, string? ClassName), nint> Windows { get; } = new();

    public Dictionary<nint, (int Left, int Top)?> Bounds { get; } = new();

    public HashSet<nint> ElevatedWindows { get; } = [];

    public List<nint> Activations { get; } = [];

    public int FindWindowCallCount { get; private set; }

    public nint? FindWindow(string? titleContains, string? className)
    {
        FindWindowCallCount++;
        return Windows.TryGetValue((titleContains, className), out var handle) ? handle : null;
    }

    public (int Left, int Top)? GetTopLeft(nint window) => Bounds.GetValueOrDefault(window);

    public bool Activate(nint window)
    {
        Activations.Add(window);
        return true;
    }

    public bool? IsProbablyElevated(nint window) => ElevatedWindows.Contains(window) ? true : null;
}

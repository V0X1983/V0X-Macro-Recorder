using V0XMacroRecorder.Core.Abstractions;

namespace V0XMacroRecorder.Tests.Fakes;

/// <summary>Couleurs connues à l'avance ; mutable en cours de lecture pour simuler un pixel qui change (Wait).</summary>
public sealed class FakePixelReader : IPixelReader
{
    public Dictionary<(int X, int Y), (byte R, byte G, byte B)> Colors { get; } = new();

    public (byte R, byte G, byte B)? GetPixelColor(int screenX, int screenY) =>
        Colors.TryGetValue((screenX, screenY), out var color) ? color : null;
}

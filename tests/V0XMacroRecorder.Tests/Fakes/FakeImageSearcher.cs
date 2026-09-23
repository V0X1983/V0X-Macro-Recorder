using V0XMacroRecorder.Core.Abstractions;
using V0XMacroRecorder.Core.Macros;

namespace V0XMacroRecorder.Tests.Fakes;

public sealed class FakeImageSearcher : IImageSearcher
{
    public sealed record Call(byte[] TemplatePngBytes, RectRegion? SearchRegion, int TolerancePercent);

    public List<Call> Calls { get; } = [];

    /// <summary>Résultat renvoyé par le prochain appel ; mutable en cours de lecture pour simuler une image qui apparaît.</summary>
    public (int X, int Y)? Result { get; set; }

    public (int X, int Y)? Find(byte[] templatePngBytes, RectRegion? searchRegion, int tolerancePercent)
    {
        Calls.Add(new Call(templatePngBytes, searchRegion, tolerancePercent));
        return Result;
    }
}

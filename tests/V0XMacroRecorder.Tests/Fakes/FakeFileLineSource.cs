using V0XMacroRecorder.Core.Abstractions;

namespace V0XMacroRecorder.Tests.Fakes;

public sealed class FakeFileLineSource : IFileLineSource
{
    public Dictionary<string, string[]> Files { get; } = new();

    public IEnumerable<string> ReadLines(string path) =>
        Files.TryGetValue(path, out var lines) ? lines : throw new FileNotFoundException(path);
}

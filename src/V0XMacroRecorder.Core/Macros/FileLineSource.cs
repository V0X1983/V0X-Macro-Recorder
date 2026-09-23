using V0XMacroRecorder.Core.Abstractions;

namespace V0XMacroRecorder.Core.Macros;

/// <summary>Implémentation réelle : aucune dépendance Win32, peut vivre directement en Core.</summary>
public sealed class FileLineSource : IFileLineSource
{
    public IEnumerable<string> ReadLines(string path) => File.ReadLines(path);
}

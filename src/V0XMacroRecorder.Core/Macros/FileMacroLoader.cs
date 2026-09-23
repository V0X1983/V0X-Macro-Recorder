using V0XMacroRecorder.Core.Abstractions;

namespace V0XMacroRecorder.Core.Macros;

/// <summary>Implémentation réelle : aucune dépendance Win32, peut vivre directement en Core.</summary>
public sealed class FileMacroLoader : IMacroLoader
{
    public Macro? Load(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return MacroSerializer.Deserialize(File.ReadAllText(path));
        }
        catch (MacroFormatException)
        {
            return null;
        }
    }
}

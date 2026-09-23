using V0XMacroRecorder.Core.Abstractions;
using V0XMacroRecorder.Core.Macros;

namespace V0XMacroRecorder.Tests.Fakes;

public sealed class FakeMacroLoader : IMacroLoader
{
    public Dictionary<string, Macro> Macros { get; } = new(StringComparer.OrdinalIgnoreCase);

    // MacroPlayer résout les chemins relatifs par rapport au dossier de la macro appelante (un chemin absolu après
    // le premier niveau d'imbrication) ; pour rester simple, ce faux ne compare que le nom de fichier.
    public Macro? Load(string path) => Macros.GetValueOrDefault(Path.GetFileName(path));
}

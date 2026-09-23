using V0XMacroRecorder.Core.Macros;

namespace V0XMacroRecorder.Core.Abstractions;

/// <summary>Charge une macro depuis un fichier (commande Appeler une autre macro). Null si introuvable/illisible (le Warning est du ressort de l'appelant).</summary>
public interface IMacroLoader
{
    Macro? Load(string path);
}

using V0XMacroRecorder.Core.Macros;

namespace V0XMacroRecorder.Core.Abstractions;

public enum MessageBoxResult
{
    Ok,
    Cancel,
}

/// <summary>Affiche une boîte de message bloquante (commande Sons/messages). Doit gérer elle-même le passage au thread UI.</summary>
public interface IMessageBoxService
{
    MessageBoxResult Show(string title, string text, MessageBoxKind kind);
}

using V0XMacroRecorder.Core.Macros;

namespace V0XMacroRecorder.Core.Abstractions;

/// <summary>Raccourcis clavier globaux (actifs même sans le focus de la fenêtre), ex. Ctrl+Alt+R pour l'enregistrement.</summary>
public interface IGlobalHotKeyService : IDisposable
{
    /// <summary>Enregistre le raccourci sous <paramref name="id"/> ; faux si un autre programme l'utilise déjà.</summary>
    bool TryRegister(int id, KeyModifiers modifiers, int virtualKey);

    void Unregister(int id);

    event EventHandler<int>? HotKeyPressed;
}

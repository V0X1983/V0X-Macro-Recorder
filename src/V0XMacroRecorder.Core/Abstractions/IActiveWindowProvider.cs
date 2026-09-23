using V0XMacroRecorder.Core.Recording;

namespace V0XMacroRecorder.Core.Abstractions;

/// <summary>Résout la fenêtre active (titre, classe, position) pour le repère de coordonnées « Fenêtre active ».</summary>
public interface IActiveWindowProvider
{
    ActiveWindowInfo? GetActiveWindow();
}

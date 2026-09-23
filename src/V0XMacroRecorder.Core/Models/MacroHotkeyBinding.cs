using V0XMacroRecorder.Core.Macros;

namespace V0XMacroRecorder.Core.Models;

/// <summary>
/// Raccourci global qui lance une macro depuis n'importe où (étape 6), indépendamment du raccourci
/// d'enregistrement (<see cref="Recording.RecordingOptions"/>). Persisté dans <see cref="AppSettings.MacroHotkeys"/>.
/// </summary>
public sealed class MacroHotkeyBinding
{
    public string MacroFilePath { get; set; } = "";

    public KeyModifiers Modifiers { get; set; } = KeyModifiers.Ctrl | KeyModifiers.Alt;

    /// <summary>Code de touche virtuelle Windows (VK_*).</summary>
    public int VirtualKey { get; set; }
}

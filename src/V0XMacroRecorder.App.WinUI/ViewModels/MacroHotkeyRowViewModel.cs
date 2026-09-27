using CommunityToolkit.Mvvm.ComponentModel;
using V0XMacroRecorder.Core.Macros;
using V0XMacroRecorder.Core.Models;

namespace V0XMacroRecorder.App.ViewModels;

/// <summary>Une ligne de l'éditeur de raccourcis de macros (étape 6) : copie éditable d'un <see cref="MacroHotkeyBinding"/>.</summary>
public sealed partial class MacroHotkeyRowViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FileName))]
    private string _macroFilePath;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KeyLabel))]
    private int _virtualKey;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KeyLabel))]
    private bool _ctrl = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KeyLabel))]
    private bool _alt = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KeyLabel))]
    private bool _shift;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KeyLabel))]
    private bool _win;

    /// <summary>Vrai si un autre programme utilise déjà ce raccourci (constaté au dernier <c>Enregistrer</c>).</summary>
    [ObservableProperty]
    private bool _hasConflict;

    public MacroHotkeyRowViewModel(MacroHotkeyBinding binding)
    {
        _macroFilePath = binding.MacroFilePath;
        _virtualKey = binding.VirtualKey;
        _ctrl = binding.Modifiers.HasFlag(KeyModifiers.Ctrl);
        _alt = binding.Modifiers.HasFlag(KeyModifiers.Alt);
        _shift = binding.Modifiers.HasFlag(KeyModifiers.Shift);
        _win = binding.Modifiers.HasFlag(KeyModifiers.Win);
    }

    public string FileName => System.IO.Path.GetFileName(MacroFilePath);

    public KeyModifiers Modifiers =>
        (Ctrl ? KeyModifiers.Ctrl : 0) | (Alt ? KeyModifiers.Alt : 0) | (Shift ? KeyModifiers.Shift : 0) | (Win ? KeyModifiers.Win : 0);

    public string KeyLabel => VirtualKey == 0 ? "(cliquez puis appuyez sur une touche)" : VirtualKeyNames.Format(Modifiers, VirtualKey);

    public MacroHotkeyBinding ToBinding() => new() { MacroFilePath = MacroFilePath, VirtualKey = VirtualKey, Modifiers = Modifiers };
}

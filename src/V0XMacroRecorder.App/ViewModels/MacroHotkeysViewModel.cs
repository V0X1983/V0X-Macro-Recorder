using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using V0XMacroRecorder.App.Infrastructure;
using V0XMacroRecorder.Core.Abstractions;
using V0XMacroRecorder.Core.Models;

namespace V0XMacroRecorder.App.ViewModels;

/// <summary>Éditeur des raccourcis globaux « lancer une macro depuis n'importe où » (étape 6, menu Outils).</summary>
public sealed partial class MacroHotkeysViewModel : ObservableObject
{
    private readonly ISettingsService _settings;
    private readonly MacroHotkeyManager _hotkeyManager;
    private readonly IDialogService _dialogs;

    public ObservableCollection<MacroHotkeyRowViewModel> Rows { get; } = [];

    [ObservableProperty]
    private string? _statusMessage;

    public MacroHotkeysViewModel(ISettingsService settings, MacroHotkeyManager hotkeyManager, IDialogService dialogs)
    {
        _settings = settings;
        _hotkeyManager = hotkeyManager;
        _dialogs = dialogs;
        foreach (var binding in settings.Current.MacroHotkeys)
        {
            Rows.Add(new MacroHotkeyRowViewModel(binding));
        }
    }

    [RelayCommand]
    private void AddMacro()
    {
        var path = _dialogs.PickOpenFile();
        if (path is not null)
        {
            Rows.Add(new MacroHotkeyRowViewModel(new MacroHotkeyBinding { MacroFilePath = path }));
        }
    }

    [RelayCommand]
    private void RemoveRow(MacroHotkeyRowViewModel? row)
    {
        if (row is not null)
        {
            Rows.Remove(row);
        }
    }

    /// <summary>Enregistre et applique immédiatement les raccourcis ; renvoie faux si un conflit a été détecté (voir <see cref="MacroHotkeyRowViewModel.HasConflict"/> par ligne).</summary>
    public bool Save()
    {
        _settings.Current.MacroHotkeys = Rows.Where(r => r.VirtualKey != 0).Select(r => r.ToBinding()).ToList();
        _ = _settings.SaveAsync();

        var results = _hotkeyManager.ApplyBindings();
        var boundRows = Rows.Where(r => r.VirtualKey != 0).ToList();
        var anyConflict = false;
        for (var i = 0; i < boundRows.Count; i++)
        {
            var conflict = i < results.Count && !results[i];
            boundRows[i].HasConflict = conflict;
            anyConflict |= conflict;
        }

        StatusMessage = anyConflict
            ? "Un ou plusieurs raccourcis sont déjà utilisés par une autre application (voir les lignes en rouge)."
            : "Raccourcis enregistrés.";
        return !anyConflict;
    }
}

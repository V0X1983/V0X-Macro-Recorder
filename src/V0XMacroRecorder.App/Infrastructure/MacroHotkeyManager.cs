using Microsoft.Extensions.Logging;
using V0XMacroRecorder.Core.Abstractions;

namespace V0XMacroRecorder.App.Infrastructure;

/// <summary>
/// Enregistre les raccourcis globaux « lancer une macro depuis n'importe où » (<c>AppSettings.MacroHotkeys</c>),
/// indépendamment des raccourcis Enregistrement/Arrêt d'urgence déjà gérés par
/// <see cref="V0XMacroRecorder.App.ViewModels.MacroDocumentViewModel"/> (IDs disjoints : ceux-ci commencent à 1000 pour ne jamais entrer
/// en collision avec les IDs 1/2 déjà pris).
/// </summary>
public sealed class MacroHotkeyManager(IGlobalHotKeyService hotKeys, ISettingsService settings, BackgroundMacroRunner runner, ILogger<MacroHotkeyManager> logger)
{
    private const int FirstHotkeyId = 1000;
    private int _registeredCount;

    /// <summary>À appeler au démarrage et après chaque modification de la liste dans l'éditeur de raccourcis.</summary>
    public IReadOnlyList<bool> ApplyBindings()
    {
        for (var i = 0; i < _registeredCount; i++)
        {
            hotKeys.Unregister(FirstHotkeyId + i);
        }

        var bindings = settings.Current.MacroHotkeys;
        var results = new bool[bindings.Count];
        for (var i = 0; i < bindings.Count; i++)
        {
            var binding = bindings[i];
            var ok = hotKeys.TryRegister(FirstHotkeyId + i, binding.Modifiers, binding.VirtualKey);
            results[i] = ok;
            if (!ok)
            {
                logger.LogWarning("Raccourci de macro déjà utilisé par une autre application : « {Path} ».", binding.MacroFilePath);
            }
        }

        _registeredCount = bindings.Count;
        return results;
    }

    public void Initialize()
    {
        ApplyBindings();
        hotKeys.HotKeyPressed += OnHotKeyPressed;
    }

    private void OnHotKeyPressed(object? sender, int id)
    {
        var index = id - FirstHotkeyId;
        var bindings = settings.Current.MacroHotkeys;
        if (index < 0 || index >= bindings.Count)
        {
            return;
        }

        _ = runner.RunAsync(bindings[index].MacroFilePath);
    }
}

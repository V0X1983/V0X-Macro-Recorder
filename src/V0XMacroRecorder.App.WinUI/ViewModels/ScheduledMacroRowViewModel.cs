using V0XMacroRecorder.Core.Models;

namespace V0XMacroRecorder.App.ViewModels;

/// <summary>Une ligne de la liste des macros planifiées (étape 6), reflet direct de <see cref="ScheduledMacroInfo"/>.</summary>
public sealed class ScheduledMacroRowViewModel(ScheduledMacroInfo info)
{
    public string TaskName => info.TaskName;

    public string MacroFilePath => info.MacroFilePath;

    public string Description => info.Description;

    public bool IsEnabled => info.IsEnabled;
}

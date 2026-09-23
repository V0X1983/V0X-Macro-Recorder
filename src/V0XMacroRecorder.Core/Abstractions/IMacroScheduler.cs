using V0XMacroRecorder.Core.Models;

namespace V0XMacroRecorder.Core.Abstractions;

/// <summary>
/// Crée/retire une tâche planifiée Windows par macro (étape 6), qui relance l'exécutable avec
/// <c>--play "chemin" --silent [--repeat N]</c>. Implémentation Win32 via le Planificateur de tâches (COM) ;
/// non testée unitairement (même précédent que les autres bords Win32 de ce projet — voir PROMPT.md).
/// </summary>
public interface IMacroScheduler
{
    /// <summary>Crée ou remplace la tâche planifiée de cette macro (au plus une par fichier) ; faux en cas d'échec.</summary>
    Task<bool> ConfigureAsync(MacroScheduleSpec spec, CancellationToken cancellationToken = default);

    /// <summary>Supprime la tâche planifiée de cette macro si elle existe ; vrai si supprimée ou déjà absente.</summary>
    Task<bool> RemoveAsync(string macroFilePath, CancellationToken cancellationToken = default);

    /// <summary>Liste toutes les tâches planifiées créées par V0X Macro Recorder.</summary>
    Task<IReadOnlyList<ScheduledMacroInfo>> ListAsync(CancellationToken cancellationToken = default);
}

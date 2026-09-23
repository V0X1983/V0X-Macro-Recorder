namespace V0XMacroRecorder.Core.Models;

/// <summary>Nature du déclencheur d'une macro planifiée (étape 6, Planificateur de tâches Windows).</summary>
public enum ScheduleTriggerKind
{
    /// <summary>Chaque jour, à <see cref="MacroScheduleSpec.TimeOfDay"/>.</summary>
    Daily,

    /// <summary>Chaque semaine, le jour <see cref="MacroScheduleSpec.DayOfWeek"/> à <see cref="MacroScheduleSpec.TimeOfDay"/>.</summary>
    Weekly,

    /// <summary>Toutes les <see cref="MacroScheduleSpec.IntervalMinutes"/> minutes.</summary>
    IntervalMinutes,

    /// <summary>À chaque ouverture de session Windows.</summary>
    AtLogon,
}

/// <summary>
/// Réglage d'une macro planifiée : au plus une tâche Windows par fichier macro (voir <c>IMacroScheduler</c>),
/// nommée <c>"V0XMacroRecorder Scheduled - {nom}"</c>, qui relance l'exécutable avec
/// <c>--play "chemin" --silent [--repeat N]</c>.
/// </summary>
public sealed class MacroScheduleSpec
{
    private int _intervalMinutes = 60;
    private int _repeatCount = 1;

    public string MacroFilePath { get; set; } = "";

    public ScheduleTriggerKind Kind { get; set; } = ScheduleTriggerKind.Daily;

    /// <summary>Heure du déclenchement (modes Daily/Weekly).</summary>
    public TimeSpan TimeOfDay { get; set; } = new(9, 0, 0);

    /// <summary>Jour du déclenchement (mode Weekly).</summary>
    public DayOfWeek DayOfWeek { get; set; } = DayOfWeek.Monday;

    /// <summary>Intervalle en minutes (mode IntervalMinutes), au moins 1.</summary>
    public int IntervalMinutes
    {
        get => _intervalMinutes;
        set => _intervalMinutes = Math.Max(1, value);
    }

    /// <summary>Nombre de répétitions passé en <c>--repeat</c> (1 = comportement par défaut de la macro).</summary>
    public int RepeatCount
    {
        get => _repeatCount;
        set => _repeatCount = Math.Max(1, value);
    }
}

/// <summary>Une tâche planifiée déjà enregistrée, telle que renvoyée par <c>IMacroScheduler.ListAsync</c>.</summary>
public sealed record ScheduledMacroInfo(string TaskName, string MacroFilePath, string Description, bool IsEnabled);

using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using V0XMacroRecorder.App.Infrastructure;
using V0XMacroRecorder.App.ViewModels.Editors;
using V0XMacroRecorder.Core.Abstractions;
using V0XMacroRecorder.Core.Models;

namespace V0XMacroRecorder.App.ViewModels;

/// <summary>Planificateur de macros (étape 6, menu Outils) : liste/crée/supprime des tâches Windows par macro.</summary>
public sealed partial class MacroScheduleViewModel : ObservableObject
{
    private readonly IMacroScheduler _scheduler;
    private readonly IDialogService _dialogs;

    public static IReadOnlyList<Choice<ScheduleTriggerKind>> KindChoices { get; } =
    [
        new(ScheduleTriggerKind.Daily, "Chaque jour, à une heure"),
        new(ScheduleTriggerKind.Weekly, "Chaque semaine, un jour à une heure"),
        new(ScheduleTriggerKind.IntervalMinutes, "À intervalle régulier"),
        new(ScheduleTriggerKind.AtLogon, "À l'ouverture de session"),
    ];

    public static IReadOnlyList<Choice<DayOfWeek>> DayChoices { get; } =
    [
        new(DayOfWeek.Monday, "Lundi"), new(DayOfWeek.Tuesday, "Mardi"), new(DayOfWeek.Wednesday, "Mercredi"),
        new(DayOfWeek.Thursday, "Jeudi"), new(DayOfWeek.Friday, "Vendredi"), new(DayOfWeek.Saturday, "Samedi"),
        new(DayOfWeek.Sunday, "Dimanche"),
    ];

    public ObservableCollection<ScheduledMacroRowViewModel> Rows { get; } = [];

    [ObservableProperty]
    private string? _newMacroFilePath;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsTime), nameof(ShowsDay), nameof(ShowsInterval))]
    private ScheduleTriggerKind _newKind;

    [ObservableProperty]
    private string _newTimeText = "09:00";

    [ObservableProperty]
    private DayOfWeek _newDayOfWeek = DayOfWeek.Monday;

    [ObservableProperty]
    private string _newIntervalMinutesText = "60";

    [ObservableProperty]
    private string _newRepeatCountText = "1";

    [ObservableProperty]
    private string? _statusMessage;

    public MacroScheduleViewModel(IMacroScheduler scheduler, IDialogService dialogs)
    {
        _scheduler = scheduler;
        _dialogs = dialogs;
        _ = RefreshAsync();
    }

    public bool ShowsTime => NewKind is ScheduleTriggerKind.Daily or ScheduleTriggerKind.Weekly;

    public bool ShowsDay => NewKind == ScheduleTriggerKind.Weekly;

    public bool ShowsInterval => NewKind == ScheduleTriggerKind.IntervalMinutes;

    [RelayCommand]
    private void BrowseMacro()
    {
        var path = _dialogs.PickOpenFile();
        if (path is not null)
        {
            NewMacroFilePath = path;
        }
    }

    [RelayCommand]
    private async Task AddAsync()
    {
        if (string.IsNullOrEmpty(NewMacroFilePath))
        {
            StatusMessage = "Choisissez d'abord un fichier macro.";
            return;
        }

        if (ShowsTime && !TimeSpan.TryParse(NewTimeText, CultureInfo.InvariantCulture, out _))
        {
            StatusMessage = "Heure invalide (attendu HH:mm).";
            return;
        }

        var spec = new MacroScheduleSpec
        {
            MacroFilePath = NewMacroFilePath,
            Kind = NewKind,
            TimeOfDay = TimeSpan.TryParse(NewTimeText, CultureInfo.InvariantCulture, out var t) ? t : new TimeSpan(9, 0, 0),
            DayOfWeek = NewDayOfWeek,
            IntervalMinutes = int.TryParse(NewIntervalMinutesText, out var interval) ? interval : 60,
            RepeatCount = int.TryParse(NewRepeatCountText, out var repeat) ? repeat : 1,
        };

        var ok = await _scheduler.ConfigureAsync(spec);
        StatusMessage = ok
            ? "Planification enregistrée."
            : "Échec de la planification : le Planificateur de tâches Windows est-il disponible ?";
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task RemoveAsync(ScheduledMacroRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        await _scheduler.RemoveAsync(row.MacroFilePath);
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        Rows.Clear();
        foreach (var info in await _scheduler.ListAsync())
        {
            Rows.Add(new ScheduledMacroRowViewModel(info));
        }
    }
}

using CommunityToolkit.Mvvm.ComponentModel;
using V0XMacroRecorder.App.ViewModels.Editors;
using V0XMacroRecorder.Core.Abstractions;
using V0XMacroRecorder.Core.Playback;

namespace V0XMacroRecorder.App.ViewModels;

/// <summary>Réglages du menu déroulant du bouton LECTURE ; chaque changement est aussitôt persisté (comme le thème).</summary>
public partial class PlaybackOptionsViewModel : ObservableObject
{
    private readonly ISettingsService _settings;
    private bool _loading = true;

    public static IReadOnlyList<Choice<WindowNotFoundAction>> WindowNotFoundChoices { get; } =
    [
        new(WindowNotFoundAction.WaitAndRetry, "Attendre puis ignorer"),
        new(WindowNotFoundAction.Ignore, "Ignorer la commande"),
        new(WindowNotFoundAction.Stop, "Arrêter la lecture"),
    ];

    [ObservableProperty] private double _speedMultiplier;
    [ObservableProperty] private bool _noDelay;
    [ObservableProperty] private int _repeatCount;
    [ObservableProperty] private bool _infiniteLoop;
    [ObservableProperty] private int _delayBetweenRepeatsMs;
    [ObservableProperty] private WindowNotFoundAction _windowNotFoundAction;
    [ObservableProperty] private int _waitForWindowTimeoutMs;
    [ObservableProperty] private bool _stepMode;

    public PlaybackOptionsViewModel(ISettingsService settings)
    {
        _settings = settings;
        var o = settings.Current.Playback;
        _speedMultiplier = o.SpeedMultiplier;
        _noDelay = o.NoDelay;
        _repeatCount = o.RepeatCount;
        _infiniteLoop = o.InfiniteLoop;
        _delayBetweenRepeatsMs = o.DelayBetweenRepeatsMs;
        _windowNotFoundAction = o.WindowNotFoundAction;
        _waitForWindowTimeoutMs = o.WaitForWindowTimeoutMs;
        _stepMode = o.StepMode;
        _loading = false;
    }

    /// <summary>Copie immuable des réglages actuels, à passer à <see cref="V0XMacroRecorder.Core.Playback.MacroPlayer"/>.</summary>
    public PlaybackOptions ToOptions() => new()
    {
        SpeedMultiplier = SpeedMultiplier,
        NoDelay = NoDelay,
        RepeatCount = RepeatCount,
        InfiniteLoop = InfiniteLoop,
        DelayBetweenRepeatsMs = DelayBetweenRepeatsMs,
        WindowNotFoundAction = WindowNotFoundAction,
        WaitForWindowTimeoutMs = WaitForWindowTimeoutMs,
        StepMode = StepMode,
    };

    partial void OnSpeedMultiplierChanged(double value) => Save(o => o.SpeedMultiplier = value);

    partial void OnNoDelayChanged(bool value) => Save(o => o.NoDelay = value);

    partial void OnRepeatCountChanged(int value) => Save(o => o.RepeatCount = value);

    partial void OnInfiniteLoopChanged(bool value) => Save(o => o.InfiniteLoop = value);

    partial void OnDelayBetweenRepeatsMsChanged(int value) => Save(o => o.DelayBetweenRepeatsMs = value);

    partial void OnWindowNotFoundActionChanged(WindowNotFoundAction value) => Save(o => o.WindowNotFoundAction = value);

    partial void OnWaitForWindowTimeoutMsChanged(int value) => Save(o => o.WaitForWindowTimeoutMs = value);

    partial void OnStepModeChanged(bool value) => Save(o => o.StepMode = value);

    private void Save(Action<PlaybackOptions> apply)
    {
        if (_loading)
        {
            return;
        }

        apply(_settings.Current.Playback);
        _ = _settings.SaveAsync();
    }
}

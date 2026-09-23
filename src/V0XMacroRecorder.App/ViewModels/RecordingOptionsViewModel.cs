using CommunityToolkit.Mvvm.ComponentModel;
using V0XMacroRecorder.App.ViewModels.Editors;
using V0XMacroRecorder.Core.Abstractions;
using V0XMacroRecorder.Core.Macros;
using V0XMacroRecorder.Core.Recording;

namespace V0XMacroRecorder.App.ViewModels;

/// <summary>Réglages du menu déroulant du bouton ENREGISTRER ; chaque changement est aussitôt persisté (comme le thème).</summary>
public partial class RecordingOptionsViewModel : ObservableObject
{
    private readonly ISettingsService _settings;
    private bool _loading = true;

    public static IReadOnlyList<Choice<MouseSamplingMode>> MouseSamplingChoices { get; } =
    [
        new(MouseSamplingMode.ClicksAndEndpoints, "Clics et positions (recommandé)"),
        new(MouseSamplingMode.FullPath, "Trajectoire complète"),
    ];

    public static IReadOnlyList<Choice<CoordinateMode>> CoordinateModeChoices { get; } =
    [
        new(CoordinateMode.Screen, "Écran (coordonnées absolues)"),
        new(CoordinateMode.ActiveWindow, "Fenêtre active"),
        new(CoordinateMode.Relative, "Relatif à la position actuelle"),
    ];

    public static IReadOnlyList<Choice<int>> CountdownChoices { get; } =
    [
        new(0, "Immédiat"),
        new(3, "3 secondes"),
        new(5, "5 secondes"),
        new(10, "10 secondes"),
    ];

    [ObservableProperty] private bool _captureMouse;
    [ObservableProperty] private bool _captureKeyboard;
    [ObservableProperty] private bool _recordDelays;
    [ObservableProperty] private bool _recordTypedTextAsString;
    [ObservableProperty] private bool _dontRecordPasswordFields;
    [ObservableProperty] private MouseSamplingMode _mouseSampling;
    [ObservableProperty] private CoordinateMode _coordinateMode;
    [ObservableProperty] private int _startCountdownSeconds;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HotKeyLabel))]
    private bool _hotKeyCtrl;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HotKeyLabel))]
    private bool _hotKeyAlt;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HotKeyLabel))]
    private bool _hotKeyShift;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HotKeyLabel))]
    private bool _hotKeyWin;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HotKeyLabel))]
    private int _hotKeyVirtualKey;

    public RecordingOptionsViewModel(ISettingsService settings)
    {
        _settings = settings;
        var o = settings.Current.Recording;
        _captureMouse = o.CaptureMouse;
        _captureKeyboard = o.CaptureKeyboard;
        _recordDelays = o.RecordDelays;
        _recordTypedTextAsString = o.RecordTypedTextAsString;
        _dontRecordPasswordFields = o.DontRecordPasswordFields;
        _mouseSampling = o.MouseSampling;
        _coordinateMode = o.CoordinateMode;
        _startCountdownSeconds = o.StartCountdownSeconds;
        _hotKeyCtrl = o.HotKeyModifiers.HasFlag(KeyModifiers.Ctrl);
        _hotKeyAlt = o.HotKeyModifiers.HasFlag(KeyModifiers.Alt);
        _hotKeyShift = o.HotKeyModifiers.HasFlag(KeyModifiers.Shift);
        _hotKeyWin = o.HotKeyModifiers.HasFlag(KeyModifiers.Win);
        _hotKeyVirtualKey = o.HotKeyVirtualKey;
        _loading = false;
    }

    public KeyModifiers HotKeyModifiers =>
        (HotKeyCtrl ? KeyModifiers.Ctrl : 0) | (HotKeyAlt ? KeyModifiers.Alt : 0) | (HotKeyShift ? KeyModifiers.Shift : 0) | (HotKeyWin ? KeyModifiers.Win : 0);

    public string HotKeyLabel => HotKeyVirtualKey == 0 ? "(cliquez puis appuyez sur une touche)" : VirtualKeyNames.Format(HotKeyModifiers, HotKeyVirtualKey);

    partial void OnCaptureMouseChanged(bool value) => Save(o => o.CaptureMouse = value);

    partial void OnCaptureKeyboardChanged(bool value) => Save(o => o.CaptureKeyboard = value);

    partial void OnRecordDelaysChanged(bool value) => Save(o => o.RecordDelays = value);

    partial void OnRecordTypedTextAsStringChanged(bool value) => Save(o => o.RecordTypedTextAsString = value);

    partial void OnDontRecordPasswordFieldsChanged(bool value) => Save(o => o.DontRecordPasswordFields = value);

    partial void OnMouseSamplingChanged(MouseSamplingMode value) => Save(o => o.MouseSampling = value);

    partial void OnCoordinateModeChanged(CoordinateMode value) => Save(o => o.CoordinateMode = value);

    partial void OnStartCountdownSecondsChanged(int value) => Save(o => o.StartCountdownSeconds = value);

    partial void OnHotKeyCtrlChanged(bool value) => Save(o => o.HotKeyModifiers = HotKeyModifiers);

    partial void OnHotKeyAltChanged(bool value) => Save(o => o.HotKeyModifiers = HotKeyModifiers);

    partial void OnHotKeyShiftChanged(bool value) => Save(o => o.HotKeyModifiers = HotKeyModifiers);

    partial void OnHotKeyWinChanged(bool value) => Save(o => o.HotKeyModifiers = HotKeyModifiers);

    partial void OnHotKeyVirtualKeyChanged(int value) => Save(o => o.HotKeyVirtualKey = value);

    private void Save(Action<RecordingOptions> apply)
    {
        if (_loading)
        {
            return;
        }

        apply(_settings.Current.Recording);
        _ = _settings.SaveAsync();
    }
}

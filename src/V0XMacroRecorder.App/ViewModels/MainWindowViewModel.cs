using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using V0XMacroRecorder.App.Helpers;
using V0XMacroRecorder.App.Infrastructure;
using V0XMacroRecorder.App.ViewModels.Editors;
using V0XMacroRecorder.App.Views;
using V0XMacroRecorder.Core.Abstractions;
using V0XMacroRecorder.Core.Macros;
using V0XMacroRecorder.Core.Models;

namespace V0XMacroRecorder.App.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private static readonly CommandPaletteItem[] PaletteDefinitions =
    [
        new("mouse", "Souris", ""),
        new("keyboard", "Clavier", ""),

        new("wait", "Attente", "", StartsGroup: true),
        new("program", "Lancer un programme ou un fichier", ""),
        new("window", "Fenêtre", ""),
        new("clipboard", "Presse-papiers", ""),
        new("text", "Saisie de texte", ""),
        new("secureinput", "Saisie protégée", ""),
        new("pixel", "Couleur d'un pixel", ""),
        new("image", "Recherche d'image", ""),

        new("url", "Ouvrir une adresse web", "", StartsGroup: true),
        new("sound", "Jouer un son", ""),
        new("message", "Afficher un message", ""),
        new("script", "Script C#", ""),

        new("if", "Condition (Si…)", "", StartsGroup: true),
        new("loop", "Boucle", ""),
        new("variable", "Variable", ""),

        new("label", "Étiquette", "", StartsGroup: true),
        new("goto", "Aller à l'étiquette", ""),
        new("call", "Appeler une autre macro", ""),
        new("stop", "Arrêter la macro", ""),
        new("pause", "Pause", ""),
        new("comment", "Commentaire", ""),
    ];

    private readonly ISettingsService _settings;
    private readonly IStartupRegistrationService _startupRegistration;
    private readonly ISystemThemeProvider _systemTheme;
    private readonly MacroHotkeyManager _hotkeyManager;
    private readonly IMacroScheduler _scheduler;
    private readonly IDialogService _dialogs;

    public MainWindowViewModel(
        ISettingsService settings,
        MacroDocumentViewModel document,
        RecordingOptionsViewModel recordingOptions,
        IStartupRegistrationService startupRegistration,
        ISystemThemeProvider systemTheme,
        MacroHotkeyManager hotkeyManager,
        IMacroScheduler scheduler,
        IDialogService dialogs)
    {
        _settings = settings;
        _startupRegistration = startupRegistration;
        _systemTheme = systemTheme;
        _hotkeyManager = hotkeyManager;
        _scheduler = scheduler;
        _dialogs = dialogs;
        Document = document;
        RecordingOptions = recordingOptions;
        _themeSetting = settings.Current.Theme;
        _startWithWindows = settings.Current.StartWithWindows;
        _startMinimizedToTray = settings.Current.StartMinimizedToTray;
        _minimizeToTrayOnClose = settings.Current.MinimizeToTrayOnClose;
        _defaultMacrosFolder = settings.Current.DefaultMacrosFolder;
        _minimizeWindowOnPlay = settings.Current.MinimizeWindowOnPlay;
        _playEndOfMacroSound = settings.Current.PlayEndOfMacroSound;
        _emergencyStopCtrl = settings.Current.EmergencyStopHotKeyModifiers.HasFlag(KeyModifiers.Ctrl);
        _emergencyStopAlt = settings.Current.EmergencyStopHotKeyModifiers.HasFlag(KeyModifiers.Alt);
        _emergencyStopShift = settings.Current.EmergencyStopHotKeyModifiers.HasFlag(KeyModifiers.Shift);
        _emergencyStopWin = settings.Current.EmergencyStopHotKeyModifiers.HasFlag(KeyModifiers.Win);
        _emergencyStopVirtualKey = settings.Current.EmergencyStopHotKeyVirtualKey;

        _systemTheme.Changed += (_, _) => System.Windows.Application.Current.Dispatcher.Invoke(ReapplySystemThemeIfActive);

        CommandPalette = PaletteDefinitions
            .Select(item => item with
            {
                IsAvailable = CommandEditorViewModel.SupportedKinds.Contains(item.Key),
                // « Si »/« Boucle » insèrent une paire début+fin, jamais une commande isolée.
                Command = item.Key is "if" or "loop" ? document.InsertBlockCommand : document.InsertCommand,
            })
            .ToList();
    }

    public MacroDocumentViewModel Document { get; }

    public RecordingOptionsViewModel RecordingOptions { get; }

    public IReadOnlyList<CommandPaletteItem> CommandPalette { get; }

    public string AppName => "V0X Macro Recorder";

    public string VersionLabel { get; } =
        "v" + (System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.1.0");

    // ---------------------------------------------------------------- Thème

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDarkTheme), nameof(IsLightTheme), nameof(IsSystemTheme))]
    private string _themeSetting;

    public bool IsDarkTheme => ThemeSetting == AppSettings.DarkTheme;

    public bool IsLightTheme => ThemeSetting == AppSettings.LightTheme;

    public bool IsSystemTheme => ThemeSetting == AppSettings.SystemTheme;

    [RelayCommand]
    private async Task SetThemeAsync(string theme)
    {
        _settings.Current.Theme = theme;
        ThemeSetting = theme;
        ApplyEffectiveTheme();
        await _settings.SaveAsync();
    }

    private void ReapplySystemThemeIfActive()
    {
        if (ThemeSetting == AppSettings.SystemTheme)
        {
            ApplyEffectiveTheme();
        }
    }

    private void ApplyEffectiveTheme() =>
        ThemeManager.ApplyTheme(ThemeSetting == AppSettings.SystemTheme
            ? (_systemTheme.IsDarkThemeActive() ? AppSettings.DarkTheme : AppSettings.LightTheme)
            : ThemeSetting);

    // ---------------------------------------------------------------- Démarrage / zone de notification

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsStartMinimizedOption))]
    private bool _startWithWindows;

    [ObservableProperty]
    private bool _startMinimizedToTray;

    /// <summary>Fermer la fenêtre (bouton X) la réduit dans la zone de notification au lieu de quitter (voir <see cref="MainWindow.OnClosing"/>).</summary>
    [ObservableProperty]
    private bool _minimizeToTrayOnClose;

    public bool ShowsStartMinimizedOption => StartWithWindows;

    partial void OnStartWithWindowsChanged(bool value)
    {
        if (!_startupRegistration.Apply(value, StartMinimizedToTray))
        {
            _dialogs.ShowError("Démarrer avec Windows", "Impossible de modifier le registre (clé HKCU Run). Réessayez, ou vérifiez les permissions de votre profil Windows.");
            return;
        }

        _settings.Current.StartWithWindows = value;
        _ = _settings.SaveAsync();
    }

    partial void OnStartMinimizedToTrayChanged(bool value)
    {
        _settings.Current.StartMinimizedToTray = value;
        _ = _settings.SaveAsync();
        if (StartWithWindows)
        {
            _startupRegistration.Apply(true, value); // Réenregistre avec/sans --minimized.
        }
    }

    partial void OnMinimizeToTrayOnCloseChanged(bool value)
    {
        _settings.Current.MinimizeToTrayOnClose = value;
        _ = _settings.SaveAsync();
    }

    // ---------------------------------------------------------------- Lecture

    [ObservableProperty]
    private bool _minimizeWindowOnPlay;

    [ObservableProperty]
    private bool _playEndOfMacroSound;

    partial void OnMinimizeWindowOnPlayChanged(bool value)
    {
        _settings.Current.MinimizeWindowOnPlay = value;
        _ = _settings.SaveAsync();
    }

    partial void OnPlayEndOfMacroSoundChanged(bool value)
    {
        _settings.Current.PlayEndOfMacroSound = value;
        _ = _settings.SaveAsync();
    }

    // ---------------------------------------------------------------- Dossier des macros

    [ObservableProperty]
    private string? _defaultMacrosFolder;

    [RelayCommand]
    private void BrowseMacrosFolder()
    {
        var folder = _dialogs.PickFolder("Dossier des macros par défaut");
        if (folder is not null)
        {
            DefaultMacrosFolder = folder;
            _settings.Current.DefaultMacrosFolder = folder;
            _ = _settings.SaveAsync();
        }
    }

    // ---------------------------------------------------------------- Raccourci d'enregistrement

    [ObservableProperty]
    private string? _recordHotkeyStatusMessage;

    [RelayCommand]
    private void ApplyRecordingHotkey()
    {
        if (RecordingOptions.HotKeyVirtualKey == 0)
        {
            RecordHotkeyStatusMessage = "Choisissez d'abord une touche.";
            return;
        }

        RecordHotkeyStatusMessage = Document.ApplyHotKey()
            ? "Raccourci enregistré."
            : "Ce raccourci est déjà utilisé par une autre application.";
    }

    // ---------------------------------------------------------------- Raccourci d'arrêt d'urgence

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EmergencyStopKeyLabel))]
    private bool _emergencyStopCtrl;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EmergencyStopKeyLabel))]
    private bool _emergencyStopAlt;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EmergencyStopKeyLabel))]
    private bool _emergencyStopShift;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EmergencyStopKeyLabel))]
    private bool _emergencyStopWin;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EmergencyStopKeyLabel))]
    private int _emergencyStopVirtualKey;

    [ObservableProperty]
    private string? _emergencyStopStatusMessage;

    public KeyModifiers EmergencyStopModifiers =>
        (EmergencyStopCtrl ? KeyModifiers.Ctrl : 0) | (EmergencyStopAlt ? KeyModifiers.Alt : 0)
        | (EmergencyStopShift ? KeyModifiers.Shift : 0) | (EmergencyStopWin ? KeyModifiers.Win : 0);

    public string EmergencyStopKeyLabel => EmergencyStopVirtualKey == 0
        ? "(cliquez puis appuyez sur une touche)"
        : VirtualKeyNames.Format(EmergencyStopModifiers, EmergencyStopVirtualKey);

    [RelayCommand]
    private void ApplyEmergencyStopHotkey()
    {
        if (EmergencyStopVirtualKey == 0)
        {
            EmergencyStopStatusMessage = "Choisissez d'abord une touche.";
            return;
        }

        _settings.Current.EmergencyStopHotKeyModifiers = EmergencyStopModifiers;
        _settings.Current.EmergencyStopHotKeyVirtualKey = EmergencyStopVirtualKey;
        _ = _settings.SaveAsync();

        EmergencyStopStatusMessage = Document.ApplyEmergencyStopHotKey()
            ? "Raccourci enregistré."
            : "Ce raccourci est déjà utilisé par une autre application.";
    }

    // ---------------------------------------------------------------- Aide / Paramètres

    [RelayCommand]
    private void OpenSettings()
    {
        var window = new SettingsWindow(this) { Owner = System.Windows.Application.Current.MainWindow };
        window.ShowDialog();
    }

    [RelayCommand]
    private void ShowHelp()
    {
        var window = new HelpWindow(_dialogs) { Owner = System.Windows.Application.Current.MainWindow };
        window.Show();
    }

    [RelayCommand]
    private void ManageMacroHotkeys()
    {
        var viewModel = new MacroHotkeysViewModel(_settings, _hotkeyManager, _dialogs);
        var window = new MacroHotkeysWindow(viewModel) { Owner = System.Windows.Application.Current.MainWindow };
        window.ShowDialog();
    }

    [RelayCommand]
    private void ManageMacroSchedule()
    {
        var viewModel = new MacroScheduleViewModel(_scheduler, _dialogs);
        var window = new MacroScheduleWindow(viewModel) { Owner = System.Windows.Application.Current.MainWindow };
        window.ShowDialog();
    }

    /// <summary>Ferme réellement l'application (jamais redirigé vers la zone de notification, voir <see cref="MainWindow.ExitForReal"/>).</summary>
    [RelayCommand]
    private static void Exit()
    {
        if (System.Windows.Application.Current.MainWindow is MainWindow window)
        {
            window.ExitForReal();
        }
        else
        {
            System.Windows.Application.Current.MainWindow?.Close();
        }
    }
}

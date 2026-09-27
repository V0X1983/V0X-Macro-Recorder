using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using V0XMacroRecorder.App.Helpers;
using V0XMacroRecorder.App.Infrastructure;
using V0XMacroRecorder.App.ViewModels.Editors;
using V0XMacroRecorder.Core.Abstractions;
using V0XMacroRecorder.Core.Macros;
using V0XMacroRecorder.Core.Models;

namespace V0XMacroRecorder.App.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private static readonly CommandPaletteItem[] PaletteDefinitions =
    [
        new("mouse", "Souris", ""),
        new("keyboard", "Clavier", ""),

        new("wait", "Attente", "", StartsGroup: true),
        new("program", "Lancer un programme ou un fichier", ""),
        new("window", "Fenêtre", ""),
        new("clipboard", "Presse-papiers", ""),
        new("text", "Saisie de texte", ""),
        new("secureinput", "Saisie protégée", ""),
        new("pixel", "Couleur d'un pixel", ""),
        new("image", "Recherche d'image", ""),

        new("url", "Ouvrir une adresse web", "", StartsGroup: true),
        new("sound", "Jouer un son", ""),
        new("message", "Afficher un message", ""),
        new("script", "Script C#", ""),

        new("if", "Condition (Si…)", "", StartsGroup: true),
        new("loop", "Boucle", ""),
        new("variable", "Variable", ""),

        new("label", "Étiquette", "", StartsGroup: true),
        new("goto", "Aller à l'étiquette", ""),
        new("call", "Appeler une autre macro", ""),
        new("stop", "Arrêter la macro", ""),
        new("pause", "Pause", ""),
        new("comment", "Commentaire", ""),
    ];

    private readonly ISettingsService _settings;
    private readonly IStartupRegistrationService _startupRegistration;
    private readonly ISystemThemeProvider _systemTheme;
    private readonly MacroHotkeyManager _hotkeyManager;
    private readonly IMacroScheduler _scheduler;
    private readonly IDialogService _dialogs;
    private readonly IUpdateChecker _updateChecker;
    private readonly ILogger<MainWindowViewModel> _logger;

    private readonly IAppWindowProvider _appWindow;

    public MainWindowViewModel(
        ISettingsService settings,
        MacroDocumentViewModel document,
        RecordingOptionsViewModel recordingOptions,
        IStartupRegistrationService startupRegistration,
        ISystemThemeProvider systemTheme,
        MacroHotkeyManager hotkeyManager,
        IMacroScheduler scheduler,
        IDialogService dialogs,
        IUpdateChecker updateChecker,
        IAppWindowProvider appWindow,
        ILogger<MainWindowViewModel> logger)
    {
        _settings = settings;
        _startupRegistration = startupRegistration;
        _systemTheme = systemTheme;
        _hotkeyManager = hotkeyManager;
        _scheduler = scheduler;
        _dialogs = dialogs;
        _updateChecker = updateChecker;
        _appWindow = appWindow;
        _logger = logger;
        Document = document;
        RecordingOptions = recordingOptions;
        _themeSetting = settings.Current.Theme;
        _updateCheckOwner = settings.Current.UpdateCheckOwner;
        _updateCheckRepo = settings.Current.UpdateCheckRepo;
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

        _systemTheme.Changed += (_, _) => _appWindow.DispatcherQueue.TryEnqueue(ReapplySystemThemeIfActive);

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
            _ = _dialogs.ShowErrorAsync("Démarrer avec Windows", "Impossible de modifier le registre (clé HKCU Run). Réessayez, ou vérifiez les permissions de votre profil Windows.");
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
    private async Task BrowseMacrosFolderAsync()
    {
        var folder = await _dialogs.PickFolderAsync("Dossier des macros par défaut");
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

    // ---------------------------------------------------------------- Mise à jour (GitHub Releases)

    [ObservableProperty]
    private string _updateCheckOwner;

    [ObservableProperty]
    private string _updateCheckRepo;

    [ObservableProperty]
    private string? _updateStatusMessage;

    [ObservableProperty]
    private string? _latestReleaseUrl;

    [ObservableProperty]
    private bool _isCheckingForUpdates;

    partial void OnUpdateCheckOwnerChanged(string value)
    {
        _settings.Current.UpdateCheckOwner = value;
        _ = _settings.SaveAsync();
    }

    partial void OnUpdateCheckRepoChanged(string value)
    {
        _settings.Current.UpdateCheckRepo = value;
        _ = _settings.SaveAsync();
    }

    [RelayCommand(CanExecute = nameof(CanCheckForUpdates))]
    private async Task CheckForUpdatesAsync()
    {
        IsCheckingForUpdates = true;
        UpdateStatusMessage = "Vérification en cours...";
        LatestReleaseUrl = null;

        try
        {
            var result = await Task.Run(() => _updateChecker.GetLatestReleaseAsync(UpdateCheckOwner, UpdateCheckRepo));
            UpdateStatusMessage = result.Message;
            LatestReleaseUrl = result.ReleaseUrl;

            if (!result.Success)
            {
                return;
            }

            var installed = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            if (!Version.TryParse(result.Version, out var latest) || installed is null || latest <= new Version(installed.Major, installed.Minor, Math.Max(installed.Build, 0)))
            {
                UpdateStatusMessage = $"V0X Macro Recorder est à jour (version {installed?.ToString(3)}).";
                return;
            }

            if (string.IsNullOrWhiteSpace(result.InstallerUrl))
            {
                UpdateStatusMessage = $"La version {result.Version} est disponible, mais aucun installeur n'y est joint : utilisez « Ouvrir la version ».";
                return;
            }

            UpdateStatusMessage = $"Version {result.Version} trouvée. Téléchargement...";
            var progress = new Progress<double>(p => UpdateStatusMessage = $"Version {result.Version} : téléchargement {p:P0}...");
            var installerPath = await Task.Run(() => _updateChecker.DownloadInstallerAsync(result, progress));

            UpdateStatusMessage = "Installation de la mise à jour : V0X Macro Recorder va se fermer puis se relancer.";
            LaunchInstallerAndRestart(installerPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la vérification des mises à jour.");
            UpdateStatusMessage = $"Erreur lors de la mise à jour : {ex.Message}";
        }
        finally
        {
            IsCheckingForUpdates = false;
        }
    }

    private bool CanCheckForUpdates() => !IsCheckingForUpdates;

    partial void OnIsCheckingForUpdatesChanged(bool value) => CheckForUpdatesCommand.NotifyCanExecuteChanged();

    /// <summary>
    /// Lance l'installeur en silencieux (avec élévation UAC seulement si nécessaire, voir <c>PrivilegesRequiredOverridesAllowed</c>
    /// dans <c>installer\V0XMacroRecorder.iss</c>) puis relance l'application, dans un PowerShell indépendant qui survit
    /// à la fermeture de V0X Macro Recorder. Si l'élévation est refusée, l'application se relance quand même, sans mise à jour.
    /// TODO Phase 4 packaging : ce flux entier (télécharger+lancer un installeur Inno Setup) est incompatible avec un
    /// paquet MSIX — à remplacer par le flux .appinstaller natif (voir plan de migration).
    /// </summary>
    private static void LaunchInstallerAndRestart(string installerPath)
    {
        static string Q(string value) => value.Replace("'", "''");

        var appPath = Environment.ProcessPath ?? string.Empty;
        var script =
            $"try {{ Start-Process -FilePath '{Q(installerPath)}' -ArgumentList '/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/CLOSEAPPLICATIONS' -Wait }} catch {{ }}; " +
            $"Start-Process -FilePath '{Q(appPath)}'";

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("powershell.exe",
            $"-NoProfile -NonInteractive -WindowStyle Hidden -ExecutionPolicy Bypass -Command \"{script}\"")
        {
            UseShellExecute = false,
            CreateNoWindow = true
        });

        Microsoft.UI.Xaml.Application.Current.Exit();
    }

    // ---------------------------------------------------------------- Aide / Paramètres
    // Une seule instance par fenêtre secondaire : contrairement à WPF (ShowDialog bloquant, un second clic sur le
    // bouton ne pouvait pas s'exécuter avant la fermeture du dialogue), ces fenêtres WinUI 3 sont non modales
    // (Activate) — sans ce suivi, cliquer plusieurs fois sur « Paramètres »/« Aide »/etc. ouvrait autant de fenêtres
    // séparées (bug réel signalé par l'utilisateur). Le clic suivant réactive juste la fenêtre déjà ouverte.

    private Views.SettingsWindow? _settingsWindow;
    private Views.HelpWindow? _helpWindow;
    private Views.MacroHotkeysWindow? _hotkeysWindow;
    private Views.MacroScheduleWindow? _scheduleWindow;

    private static void ShowOrActivate<TWindow>(TWindow? existing, Func<TWindow> factory, Action<TWindow?> store)
        where TWindow : Microsoft.UI.Xaml.Window
    {
        if (existing is null)
        {
            var window = factory();
            window.Closed += (_, _) => store(null);
            store(window);
            existing = window;
        }

        existing.Activate();
    }

    [RelayCommand]
    private void OpenSettings() => ShowOrActivate(_settingsWindow, () => new Views.SettingsWindow(this), w => _settingsWindow = w);

    [RelayCommand]
    private void ShowHelp() => ShowOrActivate(_helpWindow, () => new Views.HelpWindow(_dialogs), w => _helpWindow = w);

    [RelayCommand]
    private void ManageMacroHotkeys() => ShowOrActivate(_hotkeysWindow, () =>
        new Views.MacroHotkeysWindow(new MacroHotkeysViewModel(_settings, _hotkeyManager, _dialogs)), w => _hotkeysWindow = w);

    [RelayCommand]
    private void ManageMacroSchedule() => ShowOrActivate(_scheduleWindow, () =>
        new Views.MacroScheduleWindow(new MacroScheduleViewModel(_scheduler, _dialogs)), w => _scheduleWindow = w);

    /// <summary>
    /// Ferme réellement l'application (jamais redirigé vers la zone de notification) : délégué à <c>MainWindow</c>
    /// via cet événement plutôt qu'un appel direct à <c>_appWindow.MainWindow.Close()</c>, qui déclencherait
    /// <c>AppWindow.Closing</c> comme un clic sur le bouton X (et serait donc redirigé vers la zone de notification
    /// si l'utilisateur a activé cette option) — voir <c>MainWindow.ExitForReal</c>.
    /// </summary>
    public event EventHandler? ExitRequested;

    [RelayCommand]
    private void Exit() => ExitRequested?.Invoke(this, EventArgs.Empty);
}

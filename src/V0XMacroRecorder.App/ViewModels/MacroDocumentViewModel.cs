using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using V0XMacroRecorder.App.Infrastructure;
using V0XMacroRecorder.Core.Abstractions;
using V0XMacroRecorder.Core.Macros;
using V0XMacroRecorder.Core.Playback;
using V0XMacroRecorder.Core.Recording;

namespace V0XMacroRecorder.App.ViewModels;

/// <summary>
/// Le document ouvert : la macro en cours d'édition (via <see cref="MacroEditor"/>), son fichier, la sélection dans
/// la grille, l'enregistrement et la lecture souris/clavier en direct et toutes les actions Fichier / Édition / Insérer.
/// </summary>
public partial class MacroDocumentViewModel : ObservableObject
{
    private const string AppTitle = "V0X Macro Recorder";
    private const int RecordHotKeyId = 1;
    private const int StopPlaybackHotKeyId = 2;

    private readonly MacroEditor _editor = new();
    private readonly IDialogService _dialogs;
    private readonly ISettingsService _settings;
    private readonly IInputRecorder _recorder;
    private readonly IGlobalHotKeyService _hotKeys;
    private readonly IInputSimulator _inputSimulator;
    private readonly IWindowFinder _windowFinder;
    private readonly IElevationService _elevation;
    private readonly IKeyWaiter _keyWaiter;
    private readonly IClipboardService _clipboard;
    private readonly IProcessLauncher _launcher;
    private readonly IWindowController _windowController;
    private readonly IPixelReader _pixelReader;
    private readonly ISoundPlayer _soundPlayer;
    private readonly IMessageBoxService _messageBox;
    private readonly IImageSearcher _imageSearcher;
    private readonly IFileLineSource _fileLines;
    private readonly IMacroLoader _macroLoader;
    private readonly IScriptRunner _scriptRunner;
    private readonly ISessionLockService _sessionLock;
    private readonly ISecureInputPrompter _securePrompter;
    private readonly IDataProtector _dataProtector;
    private readonly IDisplayInfoProvider _displayInfo;
    private readonly Infrastructure.BackgroundMacroRunner _backgroundRunner;
    private readonly PlaybackOptionsViewModel _playbackOptionsViewModel;
    private readonly Dispatcher _dispatcher;
    private readonly ILogger<MacroDocumentViewModel> _logger;
    private IReadOnlyList<int> _selection = [];
    private BlockMap? _blockMap;
    /// <summary>Mot de passe du fichier ouvert/enregistré, gardé en mémoire pour la session seulement ; null = fichier non protégé. Réinitialisé par <see cref="New"/> et écrasé par <see cref="OpenFile"/>.</summary>
    private string? _protectionPassword;
    private bool _recordingBatchOpen;
    private MacroPlayer? _player;
    private CancellationTokenSource? _playbackCts;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private ObservableCollection<CommandRowViewModel> _rows = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayName), nameof(WindowTitle))]
    private string? _filePath;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEditWhileRecording), nameof(RecordButtonLabel), nameof(WindowTitle), nameof(StatusText))]
    [NotifyCanExecuteChangedFor(nameof(NewCommand), nameof(OpenCommand), nameof(OpenRecentCommand), nameof(SaveCommand),
        nameof(SaveAsCommand), nameof(SaveAsProtectedCommand), nameof(UndoCommand), nameof(RedoCommand), nameof(InsertCommand), nameof(EditSelectedCommand),
        nameof(CopyCommand), nameof(CutCommand), nameof(PasteCommand), nameof(DeleteCommand), nameof(TogglePlaybackCommand))]
    private bool _isRecording;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RecordButtonLabel))]
    private int _countdownSeconds;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEditWhileRecording), nameof(PlayButtonLabel), nameof(StatusText))]
    [NotifyCanExecuteChangedFor(nameof(NewCommand), nameof(OpenCommand), nameof(OpenRecentCommand), nameof(SaveCommand),
        nameof(SaveAsCommand), nameof(SaveAsProtectedCommand), nameof(UndoCommand), nameof(RedoCommand), nameof(InsertCommand), nameof(EditSelectedCommand),
        nameof(CopyCommand), nameof(CutCommand), nameof(PasteCommand), nameof(DeleteCommand), nameof(ToggleRecordingCommand),
        nameof(TogglePauseCommand))]
    private bool _isPlaying;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PauseButtonLabel), nameof(StatusText))]
    private bool _isPaused;

    [ObservableProperty]
    private string? _playbackWarning;

    public MacroDocumentViewModel(
        IDialogService dialogs,
        ISettingsService settings,
        IInputRecorder recorder,
        IGlobalHotKeyService hotKeys,
        IInputSimulator inputSimulator,
        IWindowFinder windowFinder,
        IElevationService elevation,
        IKeyWaiter keyWaiter,
        IClipboardService clipboard,
        IProcessLauncher launcher,
        IWindowController windowController,
        IPixelReader pixelReader,
        ISoundPlayer soundPlayer,
        IMessageBoxService messageBox,
        IImageSearcher imageSearcher,
        IFileLineSource fileLines,
        IMacroLoader macroLoader,
        IScriptRunner scriptRunner,
        ISessionLockService sessionLock,
        ISecureInputPrompter securePrompter,
        IDataProtector dataProtector,
        IDisplayInfoProvider displayInfo,
        Infrastructure.BackgroundMacroRunner backgroundRunner,
        PlaybackOptionsViewModel playbackOptions,
        ILogger<MacroDocumentViewModel> logger)
    {
        _dialogs = dialogs;
        _settings = settings;
        _recorder = recorder;
        _hotKeys = hotKeys;
        _inputSimulator = inputSimulator;
        _windowFinder = windowFinder;
        _elevation = elevation;
        _keyWaiter = keyWaiter;
        _clipboard = clipboard;
        _launcher = launcher;
        _windowController = windowController;
        _pixelReader = pixelReader;
        _soundPlayer = soundPlayer;
        _messageBox = messageBox;
        _imageSearcher = imageSearcher;
        _fileLines = fileLines;
        _macroLoader = macroLoader;
        _scriptRunner = scriptRunner;
        _sessionLock = sessionLock;
        _securePrompter = securePrompter;
        _dataProtector = dataProtector;
        _displayInfo = displayInfo;
        _backgroundRunner = backgroundRunner;
        _playbackOptionsViewModel = playbackOptions;
        _dispatcher = System.Windows.Application.Current.Dispatcher;
        _logger = logger;

        _editor.Changed += (_, _) => Refresh();
        _editor.CommandAppended += (_, command) => Rows.Add(new CommandRowViewModel(Rows.Count, command));
        RecentFiles.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasRecentFiles));
        RefreshRecentFiles();

        _recorder.CommandRecorded += OnCommandRecorded;
        _recorder.CountdownTick += (_, remaining) => Dispatch(() => CountdownSeconds = remaining);
        _hotKeys.HotKeyPressed += (_, id) =>
        {
            if (id == RecordHotKeyId)
            {
                Dispatch(() => ToggleRecordingCommand.Execute(null));
            }
            else if (id == StopPlaybackHotKeyId)
            {
                Dispatch(StopPlayback);
                _backgroundRunner.Stop(); // Arrêt d'urgence global : coupe aussi un run en arrière-plan (hotkey de macro, planificateur).
            }
        };
        ApplyHotKey();
        ApplyEmergencyStopHotKey();
    }

    /// <summary>Réenregistre le raccourci global d'enregistrement : à rappeler si l'utilisateur le change dans les options.</summary>
    public bool ApplyHotKey()
    {
        var options = _settings.Current.Recording;
        _hotKeys.Unregister(RecordHotKeyId);
        var ok = _hotKeys.TryRegister(RecordHotKeyId, options.HotKeyModifiers, options.HotKeyVirtualKey);
        _logger.LogInformation("Raccourci d'enregistrement enregistré : {Ok} ({Modifiers}+{VirtualKey:X2}).", ok, options.HotKeyModifiers, options.HotKeyVirtualKey);
        if (!ok)
        {
            _logger.LogWarning("Le raccourci d'enregistrement est déjà utilisé par une autre application.");
        }

        return ok;
    }

    /// <summary>Réenregistre le raccourci global d'arrêt d'urgence (étape 7 : devenu configurable depuis les Paramètres, Ctrl+Alt+S par défaut).</summary>
    public bool ApplyEmergencyStopHotKey()
    {
        _hotKeys.Unregister(StopPlaybackHotKeyId);
        var ok = _hotKeys.TryRegister(StopPlaybackHotKeyId, _settings.Current.EmergencyStopHotKeyModifiers, _settings.Current.EmergencyStopHotKeyVirtualKey);
        _logger.LogInformation("Raccourci d'arrêt d'urgence enregistré : {Ok} ({Modifiers}+{VirtualKey:X2}).", ok, _settings.Current.EmergencyStopHotKeyModifiers, _settings.Current.EmergencyStopHotKeyVirtualKey);
        if (!ok)
        {
            _logger.LogWarning("Le raccourci d'arrêt d'urgence est déjà utilisé par une autre application.");
        }

        return ok;
    }

    public ObservableCollection<RecentFileItem> RecentFiles { get; } = [];

    public bool HasRecentFiles => RecentFiles.Count > 0;

    public bool IsEmpty => Rows.Count == 0;

    public bool IsDirty => _editor.IsDirty;

    public string DisplayName => FilePath is null ? "Sans titre" : Path.GetFileNameWithoutExtension(FilePath);

    public string WindowTitle => $"{DisplayName}{(IsDirty ? " *" : "")} - {AppTitle}";

    /// <summary>Barre d'état (étape 7) : durée indicative à vitesse 1×, voir <see cref="MacroDurationEstimator"/> pour ce qui n'est pas compté.</summary>
    public string EstimatedDurationText => CommandDescriber.FormatDuration(
        (int)Math.Clamp(MacroDurationEstimator.EstimateMilliseconds(_editor.Commands), 0, int.MaxValue));

    public string StatusText => IsRecording ? "Enregistrement en cours" : IsPlaying ? (IsPaused ? "Lecture en pause" : "Lecture en cours") : "Prêt";

    /// <summary>Faux pendant l'enregistrement ou la lecture : Fichier/Édition/Insérer sont gelés pour ne jamais entrer en conflit avec les commandes en train d'être capturées/rejouées.</summary>
    public bool CanEditWhileRecording => !IsRecording && !IsPlaying;

    public string RecordButtonLabel => !IsRecording ? "ENREGISTRER" : CountdownSeconds > 0 ? $"DÉMARRE DANS {CountdownSeconds}…" : "ARRÊTER";

    public string PlayButtonLabel => IsPlaying ? "ARRÊTER" : "LECTURE";

    public string PauseButtonLabel => IsPaused ? "REPRENDRE" : "PAUSE";

    public RecordingOptions RecordingOptions => _settings.Current.Recording;

    public PlaybackOptionsViewModel PlaybackOptions => _playbackOptionsViewModel;

    public bool IsElevated => _elevation.IsElevated;

    private bool CanToggleRecording => !IsPlaying;

    private bool CanTogglePlayback => !IsRecording;

    private bool CanUndo => _editor.CanUndo && CanEditWhileRecording;

    private bool CanRedo => _editor.CanRedo && CanEditWhileRecording;

    private bool HasSelection => _selection.Count > 0 && CanEditWhileRecording;

    private bool HasSingleSelection => _selection.Count == 1 && CanEditWhileRecording;

    /// <summary>Demande à la vue de sélectionner ces lignes (levé après la reconstruction de la grille).</summary>
    public event EventHandler<IReadOnlyList<int>>? SelectionRequested;

    /// <summary>Appelé par la vue quand la sélection de la grille change.</summary>
    public void SetSelection(IReadOnlyList<int> indices)
    {
        _selection = indices;
        NotifyEditCommands();
    }

    // ---------------------------------------------------------------- Fichier

    [RelayCommand(CanExecute = nameof(CanEditWhileRecording))]
    private void New()
    {
        if (ConfirmDiscardChanges())
        {
            _protectionPassword = null;
            LoadMacro(new Macro(), null);
        }
    }

    [RelayCommand(CanExecute = nameof(CanEditWhileRecording))]
    private void Open()
    {
        if (!ConfirmDiscardChanges())
        {
            return;
        }

        var path = _dialogs.PickOpenFile();
        if (path is not null)
        {
            OpenFile(path);
        }
    }

    [RelayCommand(CanExecute = nameof(CanEditWhileRecording))]
    private void OpenRecent(string? path)
    {
        if (path is not null && ConfirmDiscardChanges())
        {
            OpenFile(path);
        }
    }

    [RelayCommand(CanExecute = nameof(CanEditWhileRecording))]
    private void Save() => TrySave();

    [RelayCommand(CanExecute = nameof(CanEditWhileRecording))]
    private void SaveAs() => TrySaveAs();

    /// <summary>Enregistre une copie protégée par mot de passe (étape 8) : AES-GCM, voir <see cref="ProtectedMacroFile"/>. Le mot de passe choisi ici s'applique aussi aux « Enregistrer » suivants tant que ce fichier reste ouvert.</summary>
    [RelayCommand(CanExecute = nameof(CanEditWhileRecording))]
    private void SaveAsProtected()
    {
        var path = _dialogs.PickSaveFile(DisplayName);
        if (path is null)
        {
            return;
        }

        var password = _securePrompter.PromptForSecret("Protéger cette macro", "Choisissez un mot de passe (à retenir : il ne peut pas être récupéré s'il est oublié).");
        if (string.IsNullOrEmpty(password))
        {
            return;
        }

        var confirmation = _securePrompter.PromptForSecret("Protéger cette macro", "Confirmez le mot de passe.");
        if (password != confirmation)
        {
            _dialogs.ShowError("Protéger cette macro", "Les deux mots de passe ne correspondent pas.");
            return;
        }

        _protectionPassword = password;
        WriteTo(path);
    }

    /// <summary>Ouvre un fichier sans demander de confirmation (l'appelant s'en charge).</summary>
    public void OpenFile(string path)
    {
        string? password = null;
        try
        {
            var raw = File.ReadAllText(path, Encoding.UTF8);
            if (ProtectedMacroFile.IsProtected(raw))
            {
                password = _securePrompter.PromptForSecret("Fichier protégé", $"Mot de passe de « {Path.GetFileName(path)} ».");
                if (password is null)
                {
                    return; // Annulé par l'utilisateur.
                }

                raw = ProtectedMacroFile.Decrypt(raw, password); // Lève MacroFormatException si le mot de passe est incorrect.
            }

            var macro = MacroSerializer.Deserialize(raw);
            if (macro.Commands.Any(c => c is ScriptCommand or LaunchCommand) && !_dialogs.ConfirmOpenUntrustedMacro(Path.GetFileNameWithoutExtension(path)))
            {
                return;
            }

            _protectionPassword = password;
            LoadMacro(macro, path);
            AddRecentFile(path);
        }
        catch (MacroFormatException ex)
        {
            _logger.LogWarning(ex, "Macro illisible : {Path}", path);
            _dialogs.ShowError("Ouvrir une macro", $"Impossible d'ouvrir « {Path.GetFileName(path)} ».\n\n{ex.Message}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Fichier inaccessible : {Path}", path);
            _dialogs.ShowError("Ouvrir une macro", $"Impossible de lire « {Path.GetFileName(path)} ».\n\n{ex.Message}");
            RemoveRecentFile(path);
        }
    }

    /// <summary>Enregistre (demande un nom si nécessaire) ; faux si annulé ou en échec.</summary>
    public bool TrySave() => FilePath is null ? TrySaveAs() : WriteTo(FilePath);

    public bool TrySaveAs()
    {
        var path = _dialogs.PickSaveFile(DisplayName);
        return path is not null && WriteTo(path);
    }

    /// <summary>
    /// Avant de quitter le document (nouveau, ouvrir, fermer) : propose d'enregistrer les modifications.
    /// Faux si l'utilisateur annule ou si l'enregistrement échoue.
    /// </summary>
    public bool ConfirmDiscardChanges()
    {
        if (!_editor.IsDirty)
        {
            return true;
        }

        return _dialogs.AskSaveChanges(DisplayName) switch
        {
            UnsavedChangesChoice.Save => TrySave(),
            UnsavedChangesChoice.Discard => true,
            _ => false,
        };
    }

    // ---------------------------------------------------------------- Édition

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo() => _editor.Undo();

    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo() => _editor.Redo();

    [RelayCommand(CanExecute = nameof(CanEditWhileRecording))]
    private void Insert(string? kind)
    {
        if (kind is null)
        {
            return;
        }

        var command = _dialogs.EditCommand(kind, null, KnownLabels());
        if (command is null)
        {
            return;
        }

        var index = _editor.Insert(InsertionIndex(), [command]);
        RequestSelection([index]);
    }

    /// <summary>Icônes « Si »/« Boucle » : insère une PAIRE début+fin en une seule fois, jamais une commande isolée.</summary>
    [RelayCommand(CanExecute = nameof(CanEditWhileRecording))]
    private void InsertBlock(string? kind)
    {
        MacroCommand? endMarker = kind switch
        {
            "if" => new EndIfCommand(),
            "loop" => new EndLoopCommand(),
            _ => null,
        };

        if (endMarker is null)
        {
            return;
        }

        var command = _dialogs.EditCommand(kind!, null);
        if (command is null)
        {
            return;
        }

        var index = _editor.Insert(InsertionIndex(), [command, endMarker]);
        RequestSelection([index]);
    }

    /// <summary>Menu contextuel de la grille : ajoute un « Sinon » juste avant le « Fin si » du Si sélectionné (aucun s'il en a déjà un).</summary>
    public bool CanAddElseToSelection =>
        _blockMap is not null && _selection.Count == 1 && _editor.Commands.ElementAtOrDefault(_selection[0]) is IfCommand
        && !_blockMap.ElseIndex.ContainsKey(_selection[0]);

    public void AddElseToSelectedIf()
    {
        if (!CanAddElseToSelection || _blockMap is null)
        {
            return;
        }

        var ifIndex = _selection[0];
        if (!_blockMap.MatchingEnd.TryGetValue(ifIndex, out var endIfIndex))
        {
            return;
        }

        _editor.Insert(endIfIndex, [new ElseCommand()]);
    }

    [RelayCommand(CanExecute = nameof(HasSingleSelection))]
    private void EditSelected()
    {
        var index = _selection[0];
        var current = _editor.Commands[index];
        var updated = _dialogs.EditCommand(current.Kind, current, KnownLabels());
        if (updated is null)
        {
            return;
        }

        _editor.Replace(index, updated);
        RequestSelection([index]);
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Copy() => CopySelectionToClipboard();

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Cut()
    {
        if (CopySelectionToClipboard())
        {
            DeleteSelected();
        }
    }

    [RelayCommand(CanExecute = nameof(CanEditWhileRecording))]
    private void Paste()
    {
        var commands = MacroClipboard.TryGetCommands();
        if (commands is null)
        {
            return;
        }

        var index = _editor.Insert(InsertionIndex(), commands);
        RequestSelection(Enumerable.Range(index, commands.Count).ToList());
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Delete() => DeleteSelected();

    /// <summary>Déplace des lignes (glisser-déposer) juste avant la ligne <paramref name="insertBefore"/> (Count = à la fin).
    /// La sélection est d'abord étendue aux blocs Si/Boucle complets : un Fin si ne peut jamais être déplacé sans son Si.</summary>
    public void MoveCommands(IReadOnlyList<int> indices, int insertBefore)
    {
        var moved = _editor.Move(ExpandToFullBlocks(indices), insertBefore);
        if (moved.Count > 0)
        {
            RequestSelection(moved);
        }
    }

    private bool CopySelectionToClipboard() =>
        MacroClipboard.TrySetCommands(_selection.Select(i => _editor.Commands[i]));

    private void DeleteSelected()
    {
        var full = ExpandToFullBlocks(_selection);
        var first = full.Min();
        _editor.Delete(full);
        if (_editor.Commands.Count > 0)
        {
            RequestSelection([Math.Min(first, _editor.Commands.Count - 1)]);
        }
    }

    /// <summary>Les nouvelles commandes vont après la dernière ligne sélectionnée, sinon à la fin.</summary>
    private int InsertionIndex() => _selection.Count > 0 ? _selection.Max() + 1 : _editor.Commands.Count;

    /// <summary>Noms des étiquettes déjà posées dans la macro, pour peupler le choix « Aller à l'étiquette ».</summary>
    private IReadOnlyList<string> KnownLabels() => _editor.Commands.OfType<LabelCommand>().Select(l => l.Name).ToList();

    // ---------------------------------------------------------------- Enregistrement

    [RelayCommand(CanExecute = nameof(CanToggleRecording))]
    private async Task ToggleRecordingAsync()
    {
        if (IsRecording)
        {
            StopRecording();
        }
        else
        {
            IsRecording = true;
            var (width, height) = _displayInfo.GetVirtualScreenSize(); // Étape 8 : sert à avertir d'un écart à la lecture (résolution/DPI, écran débranché).
            _editor.Macro.RecordedScreenWidth = width;
            _editor.Macro.RecordedScreenHeight = height;
            await _recorder.StartAsync(RecordingOptions);
            if (!_recorder.IsRecording)
            {
                // Décompte annulé (Stop appelé avant le vrai démarrage) : rien n'a été capturé.
                IsRecording = false;
                CountdownSeconds = 0;
            }
        }
    }

    private void StopRecording()
    {
        _recorder.Stop(); // Les commandes ont déjà été ajoutées en direct via OnCommandRecorded.
        if (_recordingBatchOpen)
        {
            _editor.EndRecordingBatch();
            _recordingBatchOpen = false;
        }

        IsRecording = false;
        CountdownSeconds = 0;
    }

    /// <summary>Appelé (hors thread UI) à chaque commande capturée : ouvre le lot d'annulation à la 1re commande seulement.</summary>
    private void OnCommandRecorded(object? sender, MacroCommand command) => Dispatch(() =>
    {
        if (!_recordingBatchOpen)
        {
            _editor.BeginRecordingBatch();
            _recordingBatchOpen = true;
        }

        _editor.AppendRecorded(command);
    });

    /// <summary>Les événements du thread des périphériques d'entrée doivent être ramenés sur le thread d'interface avant de toucher aux collections liées.</summary>
    private void Dispatch(Action action)
    {
        if (_dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            _dispatcher.Invoke(action);
        }
    }

    // ---------------------------------------------------------------- Lecture

    [RelayCommand(CanExecute = nameof(CanTogglePlayback))]
    private async Task TogglePlaybackAsync()
    {
        if (IsPlaying)
        {
            StopPlayback();
        }
        else
        {
            await StartPlaybackAsync();
        }
    }

    [RelayCommand(CanExecute = nameof(IsPlaying))]
    private void TogglePause()
    {
        if (_player is null)
        {
            return;
        }

        if (IsPaused)
        {
            _player.Resume();
        }
        else
        {
            _player.Pause();
            IsPaused = true; // Le moteur ne notifie qu'une fois réellement en pause (entre deux commandes) ; on reflète l'intention tout de suite.
        }
    }

    /// <summary>Arrêt d'urgence : relâche automatiquement toutes les touches/boutons restés enfoncés (voir <see cref="MacroPlayer"/>).</summary>
    [RelayCommand]
    private void RelaunchElevated()
    {
        if (_elevation.RelaunchElevated())
        {
            System.Windows.Application.Current.Shutdown();
        }
    }

    /// <summary>Étape 8 : compare la résolution actuelle à celle enregistrée avec la macro (voir <see cref="Macro.RecordedScreenWidth"/>) ; null si jamais enregistrée ou inchangée.</summary>
    private string? ResolutionMismatchWarningOrNull()
    {
        var macro = _editor.Macro;
        if (macro.RecordedScreenWidth <= 0 || macro.RecordedScreenHeight <= 0)
        {
            return null;
        }

        var (width, height) = _displayInfo.GetVirtualScreenSize();
        return width != macro.RecordedScreenWidth || height != macro.RecordedScreenHeight
            ? $"Résolution différente de l'enregistrement ({macro.RecordedScreenWidth}×{macro.RecordedScreenHeight} → {width}×{height}) : les coordonnées absolues peuvent être décalées."
            : null;
    }

    private async Task StartPlaybackAsync()
    {
        if (Rows.Count == 0)
        {
            return;
        }

        var startIndex = _selection.Count > 0 ? _selection.Min() : 0;
        var options = _playbackOptionsViewModel.ToOptions();
        var commands = _editor.Commands.ToList(); // instantané : l'édition est gelée pendant la lecture (CanEditWhileRecording).

        _playbackCts = new CancellationTokenSource();
        _player = new MacroPlayer(_inputSimulator, _windowFinder, _elevation, _keyWaiter, _clipboard, _launcher, _windowController, _pixelReader, _soundPlayer, _messageBox, _imageSearcher, _fileLines, _macroLoader,
            scriptRunner: _scriptRunner, sessionLock: _sessionLock, securePrompter: _securePrompter, dataProtector: _dataProtector);
        _player.CommandStarted += OnPlaybackCommandStarted;
        _player.PausedChanged += (_, paused) => Dispatch(() => IsPaused = paused);
        _player.Warning += (_, message) => Dispatch(() => PlaybackWarning = message);

        PlaybackWarning = ResolutionMismatchWarningOrNull();
        IsPlaying = true;
        IsPaused = false;
        if (_settings.Current.MinimizeWindowOnPlay)
        {
            MinimizeRequested?.Invoke(this, EventArgs.Empty);
        }

        try
        {
            await _player.RunAsync(commands, options, startIndex, cancellationToken: _playbackCts.Token, macroFilePath: FilePath);
            if (_settings.Current.PlayEndOfMacroSound)
            {
                System.Media.SystemSounds.Asterisk.Play();
            }
        }
        catch (OperationCanceledException)
        {
            // Arrêt d'urgence (bouton, Ctrl+Alt+S) : attendu, rien à signaler à l'utilisateur.
        }
        finally
        {
            ClearCurrentRow();
            IsPlaying = false;
            IsPaused = false;
            _player = null;
            _playbackCts.Dispose();
            _playbackCts = null;
            if (_settings.Current.MinimizeWindowOnPlay)
            {
                RestoreRequested?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    /// <summary>Demande à la vue de réduire/restaurer la fenêtre (option « Réduire la fenêtre pendant la lecture », Paramètres).</summary>
    public event EventHandler? MinimizeRequested;

    public event EventHandler? RestoreRequested;

    private void StopPlayback() => _playbackCts?.Cancel();

    /// <summary>Appelé (hors thread UI) au début de chaque commande : surligne la ligne correspondante dans la grille.</summary>
    private void OnPlaybackCommandStarted(object? sender, int index) => Dispatch(() =>
    {
        ClearCurrentRow();
        if (index >= 0 && index < Rows.Count)
        {
            Rows[index].IsCurrent = true;
        }
    });

    private void ClearCurrentRow()
    {
        foreach (var row in Rows)
        {
            row.IsCurrent = false;
        }
    }

    // ---------------------------------------------------------------- Interne

    private void LoadMacro(Macro macro, string? path)
    {
        if (string.IsNullOrWhiteSpace(macro.Name) && path is not null)
        {
            macro.Name = Path.GetFileNameWithoutExtension(path);
        }

        FilePath = path;
        _editor.Load(macro);
    }

    private bool WriteTo(string path)
    {
        var temp = path + ".tmp";
        try
        {
            _editor.Macro.Name = Path.GetFileNameWithoutExtension(path);
            var json = MacroSerializer.Serialize(_editor.Macro);
            var content = _protectionPassword is null ? json : ProtectedMacroFile.Encrypt(json, _protectionPassword);
            File.WriteAllText(temp, content, new UTF8Encoding(false));
            File.Move(temp, path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Échec de l'enregistrement : {Path}", path);
            TryDelete(temp);
            _dialogs.ShowError("Enregistrer la macro", $"Impossible d'enregistrer « {Path.GetFileName(path)} ».\n\n{ex.Message}");
            return false;
        }

        FilePath = path;
        _editor.MarkClean();
        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(WindowTitle));
        AddRecentFile(path);
        return true;
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Fichier temporaire : sans conséquence s'il reste.
        }
    }

    private void Refresh()
    {
        _selection = [];
        var commands = _editor.Commands;
        var rows = commands.Select((c, i) => new CommandRowViewModel(i, c)).ToList();

        try
        {
            _blockMap = BlockMap.Build(commands);
            ComputeIndentation(commands, rows);
        }
        catch (MacroFormatException)
        {
            // Fichier incohérent (édité à la main hors de l'app) : grille à plat, pas d'indentation/pliage.
            _blockMap = null;
        }

        Rows = new ObservableCollection<CommandRowViewModel>(rows);
        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(WindowTitle));
        OnPropertyChanged(nameof(EstimatedDurationText));
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
        NotifyEditCommands();
    }

    /// <summary>Une ligne de début de bloc (Si/Boucle) est à la profondeur de ce qui l'entoure ; son corps est à +1 ;
    /// Sinon/Fin si/Fin boucle reviennent à la profondeur du début du bloc.</summary>
    private static void ComputeIndentation(IReadOnlyList<MacroCommand> commands, List<CommandRowViewModel> rows)
    {
        var depth = 0;
        for (var i = 0; i < commands.Count; i++)
        {
            var command = commands[i];
            var isCloser = command is EndIfCommand or EndLoopCommand;
            var isElse = command is ElseCommand;
            var effectiveDepth = isCloser || isElse ? Math.Max(0, depth - 1) : depth;

            rows[i].IndentLevel = effectiveDepth;
            rows[i].IsBlockStart = command is IfCommand or LoopCommand;

            if (isCloser)
            {
                depth = Math.Max(0, depth - 1);
            }
            else if (command is IfCommand or LoopCommand)
            {
                depth++;
            }
        }
    }

    /// <summary>Plie/déplie le bloc dont la ligne de début est à <paramref name="rowIndex"/> ; masque/affiche ses lignes internes.</summary>
    public void ToggleFold(int rowIndex)
    {
        if (_blockMap is null || rowIndex < 0 || rowIndex >= Rows.Count || !Rows[rowIndex].IsBlockStart)
        {
            return;
        }

        if (!_blockMap.MatchingEnd.TryGetValue(rowIndex, out var endIndex))
        {
            return;
        }

        var row = Rows[rowIndex];
        row.IsCollapsed = !row.IsCollapsed;
        UpdateVisibility();
    }

    /// <summary>Recalcule IsVisible pour toutes les lignes à partir de l'état IsCollapsed de chaque début de bloc.</summary>
    private void UpdateVisibility()
    {
        if (_blockMap is null)
        {
            return;
        }

        // Piles des bornes [début+1, fin-1] des blocs actuellement repliés.
        var collapsedRanges = Rows
            .Where(r => r.IsBlockStart && r.IsCollapsed && _blockMap.MatchingEnd.ContainsKey(r.Index))
            .Select(r => (Start: r.Index + 1, End: _blockMap.MatchingEnd[r.Index] - 1))
            .ToList();

        foreach (var row in Rows)
        {
            row.IsVisible = !collapsedRanges.Any(range => row.Index >= range.Start && row.Index <= range.End);
        }
    }

    /// <summary>
    /// Étend une sélection partielle pour qu'elle englobe toujours des blocs Si/Boucle complets (jamais un
    /// <c>Fin si</c> sans son <c>Si</c>) : utilisé avant de déplacer ou de supprimer des lignes.
    /// </summary>
    public IReadOnlyList<int> ExpandToFullBlocks(IReadOnlyList<int> indices)
    {
        if (_blockMap is null || indices.Count == 0)
        {
            return indices;
        }

        var result = new SortedSet<int>(indices);
        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var i in result.ToList())
            {
                if (i < 0 || i >= _editor.Commands.Count)
                {
                    continue;
                }

                int? start = _blockMap.MatchingStart.TryGetValue(i, out var s) ? s : null;
                int? end = _blockMap.MatchingEnd.TryGetValue(i, out var e) ? e : null;
                if (_blockMap.ElseToEnd.TryGetValue(i, out var elseEnd))
                {
                    end ??= elseEnd; // ligne Sinon : appartient aussi au bloc jusqu'à Fin si.
                }

                if (start is { } startIndex && result.Add(startIndex))
                {
                    changed = true;
                }

                if (end is { } endIndex && result.Add(endIndex))
                {
                    changed = true;
                }

                if (start is { } s2 && end is { } e2)
                {
                    for (var between = s2; between <= e2; between++)
                    {
                        if (result.Add(between))
                        {
                            changed = true;
                        }
                    }
                }
            }
        }

        return result.ToList();
    }

    private void NotifyEditCommands()
    {
        EditSelectedCommand.NotifyCanExecuteChanged();
        CopyCommand.NotifyCanExecuteChanged();
        CutCommand.NotifyCanExecuteChanged();
        DeleteCommand.NotifyCanExecuteChanged();
    }

    private void RequestSelection(IReadOnlyList<int> indices) => SelectionRequested?.Invoke(this, indices);

    private void AddRecentFile(string path)
    {
        _settings.Current.AddRecentFile(path);
        _ = _settings.SaveAsync();
        RefreshRecentFiles();
    }

    private void RemoveRecentFile(string path)
    {
        _settings.Current.RemoveRecentFile(path);
        _ = _settings.SaveAsync();
        RefreshRecentFiles();
    }

    private void RefreshRecentFiles()
    {
        RecentFiles.Clear();
        foreach (var path in _settings.Current.RecentFiles)
        {
            RecentFiles.Add(new RecentFileItem(Path.GetFileName(path), path, OpenRecentCommand));
        }
    }
}

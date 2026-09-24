using V0XMacroRecorder.Core.Abstractions;
using V0XMacroRecorder.Core.Macros;

namespace V0XMacroRecorder.Core.Playback;

/// <summary>
/// Moteur de lecture (bouton LECTURE) : exécute une liste de <see cref="MacroCommand"/> via <see cref="IInputSimulator"/>.
/// Ne dépend d'aucune API Windows directement (tout le Win32 est derrière <see cref="IInputSimulator"/>,
/// <see cref="IWindowFinder"/> et <see cref="IElevationService"/>) : entièrement testable avec des implémentations
/// factices et une horloge injectable.
///
/// Sécurité : quelle que soit la façon dont <see cref="RunAsync"/> se termine (fin normale, annulation via le
/// jeton — l'arrêt d'urgence —, ou exception), un bloc <c>finally</c> relâche systématiquement toutes les touches
/// et tous les boutons de souris que ce lecteur a laissés enfoncés (jamais de Ctrl/Maj coincé).
/// </summary>
public sealed class MacroPlayer
{
    private const int ClickHoldMs = 30;
    private const int DoubleClickGapMs = 80;
    private const int WindowPollIntervalMs = 250;

    /// <summary>Profondeur maximale d'appels imbriqués (« Appeler une autre macro ») : garde-fou contre un cycle A→B→A ou une récursion infinie.</summary>
    private const int MaxCallDepth = 16;

    private static readonly IReadOnlyDictionary<KeyModifiers, (int VirtualKey, int ScanCode, bool Extended)> Modifiers =
        new Dictionary<KeyModifiers, (int, int, bool)>
        {
            [KeyModifiers.Ctrl] = (0x11, 0x1D, false),
            [KeyModifiers.Alt] = (0x12, 0x38, false),
            [KeyModifiers.Shift] = (0x10, 0x2A, false),
            [KeyModifiers.Win] = (0x5B, 0x5B, true),
        };

    private readonly IInputSimulator _simulator;
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
    private readonly ConditionEvaluator _conditions;
    private readonly IFileLineSource _fileLines;
    private readonly IMacroLoader _macroLoader;
    private readonly IScriptRunner? _scriptRunner;
    private readonly ISessionLockService? _sessionLock;
    private readonly ISecureInputPrompter? _securePrompter;
    private readonly IDataProtector? _dataProtector;
    private string? _currentMacroPath;
    private readonly Func<int, CancellationToken, Task> _delay;
    private readonly Random _random;
    private readonly Func<DateTime> _clock;

    private readonly HashSet<(int VirtualKey, int ScanCode, bool Extended)> _heldKeys = [];
    private readonly Dictionary<(int VirtualKey, int ScanCode, bool Extended), KeyModifiers> _heldKeyModifiers = new();
    private readonly HashSet<MouseButton> _heldMouseButtons = [];
    private readonly Dictionary<int, int> _heldModifierRefCounts = new();

    private readonly VariableStore _variables = new();

    private PlaybackOptions _options = new();
    private nint? _lastActivatedWindow;
    private TaskCompletionSource<bool>? _pauseGate;
    private volatile bool _pauseRequested;

    public MacroPlayer(
        IInputSimulator simulator,
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
        Func<int, CancellationToken, Task>? delay = null,
        Random? random = null,
        Func<DateTime>? clock = null,
        IScriptRunner? scriptRunner = null,
        ISessionLockService? sessionLock = null,
        ISecureInputPrompter? securePrompter = null,
        IDataProtector? dataProtector = null)
    {
        _simulator = simulator;
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
        _delay = delay ?? ((ms, ct) => Task.Delay(ms, ct));
        _random = random ?? Random.Shared;
        _clock = clock ?? (() => DateTime.Now);
        _conditions = new ConditionEvaluator(windowFinder, pixelReader, imageSearcher, _variables);
    }

    /// <summary>La commande dont l'exécution commence (pour surligner la ligne dans la grille).</summary>
    public event EventHandler<int>? CommandStarted;

    /// <summary>Une nouvelle répétition démarre (1 = la première).</summary>
    public event EventHandler<int>? RepeatStarted;

    /// <summary>Avertissement non bloquant (fenêtre introuvable, cible probablement élevée…).</summary>
    public event EventHandler<string>? Warning;

    public event EventHandler<bool>? PausedChanged;

    public bool IsPaused => _pauseGate is not null;

    /// <summary>Demande une pause avant la prochaine commande (façon Pause/Reprendre).</summary>
    public void Pause() => _pauseRequested = true;

    /// <summary>Reprend une lecture en pause (ou avance d'un pas en mode pas à pas).</summary>
    public void Resume()
    {
        _pauseRequested = false;
        var gate = _pauseGate;
        _pauseGate = null;
        if (gate is not null)
        {
            PausedChanged?.Invoke(this, false);
            gate.TrySetResult(true);
        }
    }

    /// <summary>
    /// Exécute la macro. <paramref name="startIndex"/> ne s'applique qu'à la 1re passe (les répétitions suivantes
    /// repartent du début). <paramref name="breakpoints"/> marque des indices où la lecture se met en pause avant
    /// d'exécuter la commande. L'annulation de <paramref name="cancellationToken"/> est l'arrêt d'urgence.
    /// </summary>
    public async Task RunAsync(
        IReadOnlyList<MacroCommand> commands,
        PlaybackOptions options,
        int startIndex = 0,
        ISet<int>? breakpoints = null,
        CancellationToken cancellationToken = default,
        string? macroFilePath = null)
    {
        ArgumentNullException.ThrowIfNull(commands);
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _currentMacroPath = macroFilePath;
        ResetState();

        // Vérifié une seule fois, avant la 1re commande (pas en continu pendant la lecture) : si l'utilisateur
        // verrouille sa session ou qu'une invite UAC apparaît PENDANT une lecture déjà commencée, ce n'est détecté
        // qu'à la répétition suivante — limite documentée, pas une garantie temps réel.
        if (_sessionLock?.IsInputSessionLocked() == true)
        {
            Warning?.Invoke(this, "Session verrouillée ou écran sécurisé (UAC) actif : la lecture ne peut pas atteindre le bureau, arrêtée avant de commencer.");
            return;
        }

        try
        {
            BlockMap blocks;
            try
            {
                blocks = BlockMap.Build(commands);
            }
            catch (MacroFormatException ex)
            {
                Warning?.Invoke(this, ex.Message);
                return;
            }

            var repeat = 0;
            while (true)
            {
                repeat++;
                RepeatStarted?.Invoke(this, repeat);

                var signal = await RunFrameAsync(commands, blocks, startIndex, breakpoints, callDepth: 0, callChain: [], cancellationToken).ConfigureAwait(false);
                if (signal.Signal == ExecutionSignal.SignalKind.Stop)
                {
                    return; // Arrêt demandé par une commande (Arrêter, ou fenêtre introuvable + WindowNotFoundAction.Stop).
                }

                startIndex = 0;
                if (!_options.InfiniteLoop && repeat >= _options.RepeatCount)
                {
                    break;
                }

                await DelayAsync(ScaleDelay(_options.DelayBetweenRepeatsMs), cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            ReleaseEverything();
        }
    }

    /// <summary>
    /// Exécute une passe de <paramref name="commands"/> à partir de <paramref name="startIndex"/>, en suivant les
    /// sauts (<see cref="ExecutionSignal.SignalKind.Jump"/>) demandés par les commandes de contrôle de flux.
    /// <paramref name="callDepth"/> vaut 0 pour la macro jouée directement ; un appel imbriqué (« Appeler une autre
    /// macro ») utilise une profondeur &gt; 0 et n'émet ni surlignage de ligne (<see cref="CommandStarted"/>) ni pause,
    /// qui n'ont de sens que pour la macro affichée à l'écran.
    /// </summary>
    private async Task<ExecutionSignal> RunFrameAsync(
        IReadOnlyList<MacroCommand> commands,
        BlockMap blocks,
        int startIndex,
        ISet<int>? breakpoints,
        int callDepth,
        IReadOnlyList<string> callChain,
        CancellationToken cancellationToken)
    {
        // Un dictionnaire local par appel de RunFrameAsync : jamais de collision entre boucles imbriquées ni entre
        // un appel (« Appeler une autre macro ») et la macro appelante, chacun ayant sa propre trame.
        var loopStates = new Dictionary<int, LoopState>();

        for (var i = Math.Clamp(startIndex, 0, commands.Count); i < commands.Count;)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (callDepth == 0 && (_pauseRequested || _options.StepMode || breakpoints?.Contains(i) == true))
            {
                await WaitForResumeAsync(cancellationToken).ConfigureAwait(false);
            }

            if (callDepth == 0)
            {
                CommandStarted?.Invoke(this, i);
            }

            var command = commands[i];
            await DelayAsync(ScaleDelay(command.DelayMs), cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            var signal = await ExecuteAsync(command, i, blocks, loopStates, callDepth, callChain, cancellationToken).ConfigureAwait(false);
            switch (signal.Signal)
            {
                case ExecutionSignal.SignalKind.Stop:
                    return ExecutionSignal.Stop;

                case ExecutionSignal.SignalKind.Jump:
                    i = signal.TargetIndex;
                    break;

                default:
                    i++;
                    break;
            }
        }

        return ExecutionSignal.Continue;
    }

    private void ResetState()
    {
        _heldKeys.Clear();
        _heldKeyModifiers.Clear();
        _heldMouseButtons.Clear();
        _heldModifierRefCounts.Clear();
        _lastActivatedWindow = null;
        _pauseRequested = false;
        _pauseGate = null;
        _variables.Clear();
    }

    private Task WaitForResumeAsync(CancellationToken cancellationToken)
    {
        _pauseGate ??= new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        PausedChanged?.Invoke(this, true);
        return _pauseGate.Task.WaitAsync(cancellationToken);
    }

    // ------------------------------------------------------------------------------------------------ Exécution

    /// <summary>État d'une boucle en cours (mode RepeatCount ou ForEachLine), tenu par <see cref="RunFrameAsync"/> le temps d'une passe.</summary>
    private sealed class LoopState
    {
        public int IterationsDone;
        public IEnumerator<string>? FileLines;
    }

    private async Task<ExecutionSignal> ExecuteAsync(MacroCommand command, int index, BlockMap blocks, Dictionary<int, LoopState> loopStates, int callDepth, IReadOnlyList<string> callChain, CancellationToken ct)
    {
        switch (command)
        {
            case MouseCommand mouse:
                return await ExecuteMouseAsync(mouse, ct).ConfigureAwait(false);

            case KeyboardCommand key:
                ExecuteKeyboard(key);
                return ExecutionSignal.Continue;

            case TextCommand text:
                await ExecuteTextAsync(text, ct).ConfigureAwait(false);
                return ExecutionSignal.Continue;

            case WaitCommand wait:
                await ExecuteWaitAsync(wait, ct).ConfigureAwait(false);
                return ExecutionSignal.Continue;

            case ClipboardCommand clipboard:
                await ExecuteClipboardAsync(clipboard, ct).ConfigureAwait(false);
                return ExecutionSignal.Continue;

            case LaunchCommand launch:
                await ExecuteLaunchAsync(launch, ct).ConfigureAwait(false);
                return ExecutionSignal.Continue;

            case OpenUrlCommand url:
                await ExecuteOpenUrlAsync(url, ct).ConfigureAwait(false);
                return ExecutionSignal.Continue;

            case WindowCommand window:
                return await ExecuteWindowAsync(window, ct).ConfigureAwait(false);

            case PixelCommand pixel:
                await ExecutePixelAsync(pixel, ct).ConfigureAwait(false);
                return ExecutionSignal.Continue;

            case SoundCommand sound:
                await ExecuteSoundAsync(sound, ct).ConfigureAwait(false);
                return ExecutionSignal.Continue;

            case MessageCommand message:
                ExecuteMessage(message);
                return ExecutionSignal.Continue;

            case ImageSearchCommand image:
                await ExecuteImageSearchAsync(image, ct).ConfigureAwait(false);
                return ExecutionSignal.Continue;

            case IfCommand ifCommand:
                return ExecuteIf(ifCommand, index, blocks);

            case ElseCommand:
                // Atteint uniquement par retombée du corps vrai d'un Si : sauter par-dessus le corps Sinon.
                return ExecutionSignal.JumpTo(blocks.ElseToEnd[index] + 1);

            case EndIfCommand:
                return ExecutionSignal.Continue;

            case LoopCommand loop:
                return ExecuteLoopStart(loop, index, blocks, loopStates);

            case EndLoopCommand:
                return ExecutionSignal.JumpTo(blocks.MatchingStart[index]);

            case VariableCommand variable:
                ExecuteVariable(variable);
                return ExecutionSignal.Continue;

            case GotoCommand gotoCmd:
                if (blocks.Labels.TryGetValue(gotoCmd.TargetLabel, out var target))
                {
                    return ExecutionSignal.JumpTo(target);
                }

                Warning?.Invoke(this, $"Étiquette introuvable : « {gotoCmd.TargetLabel} ».");
                return ExecutionSignal.Continue;

            case StopCommand:
                return ExecutionSignal.Stop;

            case PauseCommand pause:
                await _keyWaiter.WaitForKeyAsync(pause.VirtualKey, pause.TimeoutMs, ct).ConfigureAwait(false);
                return ExecutionSignal.Continue;

            case CallCommand call:
                return await ExecuteCallAsync(call, callDepth, callChain, ct).ConfigureAwait(false);

            case ScriptCommand script:
                await ExecuteScriptAsync(script, ct).ConfigureAwait(false);
                return ExecutionSignal.Continue;

            case SecureInputCommand secureInput:
                await ExecuteSecureInputAsync(secureInput, ct).ConfigureAwait(false);
                return ExecutionSignal.Continue;

            default:
                // CommentCommand et LabelCommand (simple marqueur, résolu une fois pour toutes par BlockMap.Labels) :
                // aucune exécution propre nécessaire.
                return ExecutionSignal.Continue;
        }
    }

    private ExecutionSignal ExecuteIf(IfCommand ifCommand, int index, BlockMap blocks)
    {
        var conditionTrue = _conditions.Evaluate(ifCommand.Condition) != ifCommand.Negate;
        if (conditionTrue)
        {
            return ExecutionSignal.Continue; // Entre dans le corps « vrai ».
        }

        var target = blocks.ElseIndex.TryGetValue(index, out var elseIndex) ? elseIndex + 1 : blocks.MatchingEnd[index] + 1;
        return ExecutionSignal.JumpTo(target);
    }

    private ExecutionSignal ExecuteLoopStart(LoopCommand loop, int index, BlockMap blocks, Dictionary<int, LoopState> loopStates)
    {
        var afterLoop = blocks.MatchingEnd[index] + 1;

        switch (loop.Mode)
        {
            case LoopMode.While:
                if (!_conditions.Evaluate(loop.WhileCondition ?? new ConditionSpec()))
                {
                    return ExecutionSignal.JumpTo(afterLoop);
                }

                return ExecutionSignal.Continue;

            case LoopMode.ForEachLine:
                if (!loopStates.TryGetValue(index, out var lineState))
                {
                    lineState = new LoopState { FileLines = string.IsNullOrEmpty(loop.FilePath) ? null : _fileLines.ReadLines(loop.FilePath).GetEnumerator() };
                    loopStates[index] = lineState;
                }

                if (lineState.FileLines is null || !lineState.FileLines.MoveNext())
                {
                    loopStates.Remove(index);
                    return ExecutionSignal.JumpTo(afterLoop);
                }

                if (!string.IsNullOrEmpty(loop.LineVariableName))
                {
                    _variables.Set(loop.LineVariableName, lineState.FileLines.Current);
                }

                return ExecutionSignal.Continue;

            default: // RepeatCount
                if (!loopStates.TryGetValue(index, out var countState))
                {
                    countState = new LoopState();
                    loopStates[index] = countState;
                }

                if (countState.IterationsDone >= loop.RepeatCount)
                {
                    loopStates.Remove(index);
                    return ExecutionSignal.JumpTo(afterLoop);
                }

                countState.IterationsDone++;
                return ExecutionSignal.Continue;
        }
    }

    private void ExecuteVariable(VariableCommand variable)
    {
        if (string.IsNullOrEmpty(variable.Name))
        {
            return;
        }

        switch (variable.Mode)
        {
            case VariableMode.Set:
                _variables.Set(variable.Name, TokenExpander.Expand(variable.Value, _variables, _clipboard, _clock));
                break;

            case VariableMode.Increment:
                var deltaText = TokenExpander.Expand(string.IsNullOrEmpty(variable.Value) ? "1" : variable.Value, _variables, _clipboard, _clock);
                var delta = double.TryParse(deltaText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : 1;
                var current = _variables.TryGetNumber(variable.Name, out var n) ? n : 0;
                _variables.Set(variable.Name, (current + delta).ToString(System.Globalization.CultureInfo.InvariantCulture));
                break;

            case VariableMode.Calculate:
                var expression = TokenExpander.Expand(variable.Value, _variables, _clipboard, _clock);
                var result = SimpleExpressionEvaluator.Evaluate(expression);
                _variables.Set(variable.Name, result.ToString(System.Globalization.CultureInfo.InvariantCulture));
                break;
        }
    }

    /// <summary>
    /// Appelle une autre macro comme une sous-routine : méthode récursive dédiée (pas un <see cref="RunAsync"/>
    /// ré-entrant), pour que <see cref="ResetState"/>/<see cref="ReleaseEverything"/> ne se redéclenchent jamais sur
    /// un appel imbriqué — seul l'appel racine les exécute. Un <see cref="StopCommand"/> dans la macro appelée
    /// n'arrête que celle-ci (sémantique « sous-routine ») : l'exécution reprend après la commande Appeler.
    /// </summary>
    private async Task<ExecutionSignal> ExecuteCallAsync(CallCommand call, int callDepth, IReadOnlyList<string> callChain, CancellationToken ct)
    {
        if (callDepth >= MaxCallDepth)
        {
            Warning?.Invoke(this, "Profondeur d'appel maximale atteinte : appel ignoré.");
            return ExecutionSignal.Continue;
        }

        var baseDir = string.IsNullOrEmpty(_currentMacroPath) ? "" : Path.GetDirectoryName(_currentMacroPath) ?? "";
        var resolvedPath = Path.Combine(baseDir, call.MacroFilePath);
        var fullPath = Path.GetFullPath(resolvedPath);

        if (callChain.Any(p => string.Equals(p, fullPath, StringComparison.OrdinalIgnoreCase)))
        {
            Warning?.Invoke(this, $"Appel récursif détecté (« {call.MacroFilePath} ») : appel ignoré.");
            return ExecutionSignal.Continue;
        }

        var macro = _macroLoader.Load(resolvedPath);
        if (macro is null)
        {
            Warning?.Invoke(this, $"Macro introuvable ou illisible : « {call.MacroFilePath} ».");
            return ExecutionSignal.Continue;
        }

        BlockMap blocks;
        try
        {
            blocks = BlockMap.Build(macro.Commands);
        }
        catch (MacroFormatException ex)
        {
            Warning?.Invoke(this, $"« {call.MacroFilePath} » : {ex.Message}");
            return ExecutionSignal.Continue;
        }

        var previousMacroPath = _currentMacroPath;
        _currentMacroPath = fullPath;
        try
        {
            await RunFrameAsync(macro.Commands, blocks, 0, breakpoints: null, callDepth + 1, [.. callChain, fullPath], ct).ConfigureAwait(false);
        }
        finally
        {
            _currentMacroPath = previousMacroPath;
        }

        return ExecutionSignal.Continue; // Un Stop dans la macro appelée n'arrête que celle-ci (voir doc de la méthode).
    }

    private async Task<ExecutionSignal> ExecuteMouseAsync(MouseCommand mouse, CancellationToken ct)
    {
        int x, y;
        switch (mouse.CoordinateMode)
        {
            case CoordinateMode.ActiveWindow:
                var handle = await ResolveWindowAsync(mouse.WindowTitle, mouse.WindowClassName, ct).ConfigureAwait(false);
                var bounds = handle is { } h ? _windowFinder.GetTopLeft(h) : null;
                if (bounds is null)
                {
                    Warning?.Invoke(this, string.IsNullOrEmpty(mouse.WindowTitle)
                        ? "Fenêtre introuvable : commande ignorée."
                        : $"Fenêtre introuvable (« {mouse.WindowTitle} ») : commande ignorée.");
                    return _options.WindowNotFoundAction == WindowNotFoundAction.Stop
                        ? ExecutionSignal.Stop
                        : ExecutionSignal.Continue;
                }

                EnsureActivated(handle!.Value);
                x = bounds.Value.Left + mouse.X;
                y = bounds.Value.Top + mouse.Y;
                break;

            case CoordinateMode.Relative:
                var cursor = _simulator.GetCursorPosition();
                x = cursor.X + mouse.X;
                y = cursor.Y + mouse.Y;
                break;

            default:
                x = mouse.X;
                y = mouse.Y;
                break;
        }

        switch (mouse.Action)
        {
            case MouseAction.Move:
                _simulator.MoveMouseTo(x, y);
                break;

            case MouseAction.Down:
                _simulator.MoveMouseTo(x, y);
                _simulator.MouseButton(mouse.Button, true);
                _heldMouseButtons.Add(mouse.Button);
                break;

            case MouseAction.Up:
                _simulator.MoveMouseTo(x, y);
                _simulator.MouseButton(mouse.Button, false);
                _heldMouseButtons.Remove(mouse.Button);
                break;

            case MouseAction.Click:
                _simulator.MoveMouseTo(x, y);
                await ClickAsync(mouse.Button, ct).ConfigureAwait(false);
                break;

            case MouseAction.DoubleClick:
                _simulator.MoveMouseTo(x, y);
                await ClickAsync(mouse.Button, ct).ConfigureAwait(false);
                await DelayAsync(ScaleDelay(DoubleClickGapMs), ct).ConfigureAwait(false);
                await ClickAsync(mouse.Button, ct).ConfigureAwait(false);
                break;

            case MouseAction.Wheel:
                _simulator.MoveMouseTo(x, y);
                _simulator.MouseWheel(mouse.WheelDelta);
                break;
        }

        return ExecutionSignal.Continue;
    }

    private async Task ClickAsync(MouseButton button, CancellationToken ct)
    {
        _simulator.MouseButton(button, true);
        await DelayAsync(ScaleDelay(ClickHoldMs), ct).ConfigureAwait(false);
        _simulator.MouseButton(button, false);
    }

    private async Task<ExecutionSignal> ExecuteWindowAsync(WindowCommand window, CancellationToken ct)
    {
        var handle = await ResolveWindowAsync(window.WindowTitle, window.WindowClassName, ct).ConfigureAwait(false);
        if (handle is null)
        {
            Warning?.Invoke(this, string.IsNullOrEmpty(window.WindowTitle)
                ? "Fenêtre introuvable : commande ignorée."
                : $"Fenêtre introuvable (« {window.WindowTitle} ») : commande ignorée.");
            return _options.WindowNotFoundAction == WindowNotFoundAction.Stop ? ExecutionSignal.Stop : ExecutionSignal.Continue;
        }

        switch (window.Action)
        {
            case WindowAction.Activate:
                EnsureActivated(handle.Value);
                break;

            case WindowAction.Minimize:
                _windowController.Minimize(handle.Value);
                break;

            case WindowAction.Maximize:
                _windowController.Maximize(handle.Value);
                break;

            case WindowAction.Restore:
                _windowController.Restore(handle.Value);
                break;

            case WindowAction.Close:
                _windowController.Close(handle.Value);
                break;

            case WindowAction.MoveResize:
                _windowController.MoveResize(handle.Value, window.X, window.Y, window.Width, window.Height);
                break;
        }

        return ExecutionSignal.Continue;
    }

    /// <summary>Cherche la fenêtre, en réessayant pendant <see cref="PlaybackOptions.WaitForWindowTimeoutMs"/> si <see cref="WindowNotFoundAction.WaitAndRetry"/> est actif.</summary>
    private async Task<nint?> ResolveWindowAsync(string? title, string? className, CancellationToken ct)
    {
        var found = _windowFinder.FindWindow(title, className);
        if (found is not null || _options.WindowNotFoundAction != WindowNotFoundAction.WaitAndRetry)
        {
            return found;
        }

        var elapsed = 0;
        while (elapsed < _options.WaitForWindowTimeoutMs)
        {
            await _delay(WindowPollIntervalMs, ct).ConfigureAwait(false); // Attente technique : jamais mise à l'échelle par la vitesse.
            elapsed += WindowPollIntervalMs;
            found = _windowFinder.FindWindow(title, className);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private void EnsureActivated(nint handle)
    {
        if (_lastActivatedWindow == handle)
        {
            return;
        }

        if (!_elevation.IsElevated && _windowFinder.IsProbablyElevated(handle) == true)
        {
            Warning?.Invoke(this, "La fenêtre cible semble s'exécuter en administrateur : les entrées risquent de ne pas lui parvenir (limite Windows/UIPI). Relancez V0X Macro Recorder en administrateur si besoin.");
        }

        _windowFinder.Activate(handle);
        _lastActivatedWindow = handle;
    }

    private void ExecuteKeyboard(KeyboardCommand key)
    {
        var identity = (key.VirtualKey, key.ScanCode, key.IsExtendedKey);
        switch (key.Action)
        {
            case KeyAction.Press:
                PressModifiers(key.Modifiers);
                _simulator.KeyEvent(key.VirtualKey, key.ScanCode, key.IsExtendedKey, true);
                _simulator.KeyEvent(key.VirtualKey, key.ScanCode, key.IsExtendedKey, false);
                ReleaseModifiers(key.Modifiers);
                break;

            case KeyAction.Down:
                PressModifiers(key.Modifiers);
                _simulator.KeyEvent(key.VirtualKey, key.ScanCode, key.IsExtendedKey, true);
                _heldKeys.Add(identity);
                _heldKeyModifiers[identity] = key.Modifiers;
                break;

            case KeyAction.Up:
                _simulator.KeyEvent(key.VirtualKey, key.ScanCode, key.IsExtendedKey, false);
                _heldKeys.Remove(identity);
                ReleaseModifiers(_heldKeyModifiers.Remove(identity, out var heldModifiers) ? heldModifiers : key.Modifiers);
                break;
        }
    }

    /// <summary>Compte de référence par touche modificatrice : reste enfoncée tant qu'au moins une touche « réelle » en dépend encore.</summary>
    private void PressModifiers(KeyModifiers modifiers)
    {
        foreach (var flag in EnumerateFlags(modifiers))
        {
            var (virtualKey, scanCode, extended) = Modifiers[flag];
            _heldModifierRefCounts.TryGetValue(scanCode, out var count);
            if (count == 0)
            {
                _simulator.KeyEvent(virtualKey, scanCode, extended, true);
            }

            _heldModifierRefCounts[scanCode] = count + 1;
        }
    }

    private void ReleaseModifiers(KeyModifiers modifiers)
    {
        foreach (var flag in EnumerateFlags(modifiers))
        {
            var (virtualKey, scanCode, extended) = Modifiers[flag];
            if (!_heldModifierRefCounts.TryGetValue(scanCode, out var count) || count <= 0)
            {
                continue;
            }

            count--;
            if (count <= 0)
            {
                _heldModifierRefCounts.Remove(scanCode);
                _simulator.KeyEvent(virtualKey, scanCode, extended, false);
            }
            else
            {
                _heldModifierRefCounts[scanCode] = count;
            }
        }
    }

    private static IEnumerable<KeyModifiers> EnumerateFlags(KeyModifiers modifiers)
    {
        if (modifiers.HasFlag(KeyModifiers.Ctrl)) yield return KeyModifiers.Ctrl;
        if (modifiers.HasFlag(KeyModifiers.Alt)) yield return KeyModifiers.Alt;
        if (modifiers.HasFlag(KeyModifiers.Shift)) yield return KeyModifiers.Shift;
        if (modifiers.HasFlag(KeyModifiers.Win)) yield return KeyModifiers.Win;
    }

    private async Task ExecuteWaitAsync(WaitCommand wait, CancellationToken ct)
    {
        switch (wait.Mode)
        {
            case WaitMode.WindowAppears:
                await PollUntilAsync(() => _windowFinder.FindWindow(wait.WindowTitle, wait.WindowClassName) is not null, wait.TimeoutMs, ct).ConfigureAwait(false);
                break;

            case WaitMode.WindowDisappears:
                await PollUntilAsync(() => _windowFinder.FindWindow(wait.WindowTitle, wait.WindowClassName) is null, wait.TimeoutMs, ct).ConfigureAwait(false);
                break;

            case WaitMode.KeyPress:
                if (!await _keyWaiter.WaitForKeyAsync(wait.VirtualKey, wait.TimeoutMs, ct).ConfigureAwait(false))
                {
                    Warning?.Invoke(this, "Attente d'une touche : délai d'attente dépassé.");
                }

                break;

            case WaitMode.PixelMatch:
                var expected = ColorMatch.ParseHex(wait.PixelColorHex);
                await PollUntilAsync(
                    () => _pixelReader.GetPixelColor(wait.PixelX, wait.PixelY) is { } c && ColorMatch.Matches(c, expected, wait.PixelTolerancePercent),
                    wait.TimeoutMs, ct).ConfigureAwait(false);
                break;

            case WaitMode.ImageFound:
                if (!string.IsNullOrEmpty(wait.ImageTemplatePngBase64))
                {
                    var template = Convert.FromBase64String(wait.ImageTemplatePngBase64);
                    await PollUntilAsync(
                        () => _imageSearcher.Find(template, wait.ImageSearchRegion, wait.ImageTolerancePercent) is not null,
                        wait.TimeoutMs, ct).ConfigureAwait(false);
                }

                break;

            default: // FixedDelay.
                var duration = wait.RandomExtraMs > 0 ? wait.DurationMs + _random.Next(0, wait.RandomExtraMs + 1) : wait.DurationMs;
                await DelayAsync(ScaleDelay(duration), ct).ConfigureAwait(false);
                break;
        }
    }

    /// <summary>Sonde <paramref name="isSatisfied"/> toutes les <see cref="WindowPollIntervalMs"/> (jamais mis à l'échelle par la vitesse) jusqu'à ce qu'elle soit vraie ou que <paramref name="timeoutMs"/> s'écoule.</summary>
    private async Task PollUntilAsync(Func<bool> isSatisfied, int timeoutMs, CancellationToken ct)
    {
        if (isSatisfied())
        {
            return;
        }

        var elapsed = 0;
        while (elapsed < timeoutMs)
        {
            await _delay(WindowPollIntervalMs, ct).ConfigureAwait(false);
            elapsed += WindowPollIntervalMs;
            if (isSatisfied())
            {
                return;
            }
        }

        Warning?.Invoke(this, "Attente : délai d'attente dépassé.");
    }

    private async Task ExecutePixelAsync(PixelCommand pixel, CancellationToken ct)
    {
        var expected = ColorMatch.ParseHex(pixel.ExpectedColorHex);
        bool Matches() => _pixelReader.GetPixelColor(pixel.X, pixel.Y) is { } c && ColorMatch.Matches(c, expected, pixel.TolerancePercent);

        if (pixel.Mode == PixelActionMode.Wait)
        {
            await PollUntilAsync(Matches, pixel.TimeoutMs, ct).ConfigureAwait(false);
            return;
        }

        if (!string.IsNullOrEmpty(pixel.VariableName))
        {
            _variables.Set(pixel.VariableName, Matches() ? "1" : "0");
        }
    }

    private async Task ExecuteImageSearchAsync(ImageSearchCommand image, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(image.TemplatePngBase64))
        {
            return;
        }

        var template = Convert.FromBase64String(image.TemplatePngBase64);
        (int X, int Y)? found = null;
        if (image.TimeoutMs > 0)
        {
            await PollUntilAsync(() =>
            {
                found = _imageSearcher.Find(template, image.SearchRegion, image.TolerancePercent);
                return found is not null;
            }, image.TimeoutMs, ct).ConfigureAwait(false);
        }
        else
        {
            found = _imageSearcher.Find(template, image.SearchRegion, image.TolerancePercent);
        }

        if (found is not { } position)
        {
            Warning?.Invoke(this, "Recherche d'image : modèle non trouvé.");
            return;
        }

        if (!string.IsNullOrEmpty(image.FoundXVariable))
        {
            _variables.Set(image.FoundXVariable, position.X.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        if (!string.IsNullOrEmpty(image.FoundYVariable))
        {
            _variables.Set(image.FoundYVariable, position.Y.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        if (image.ClickIfFound)
        {
            _simulator.MoveMouseTo(position.X, position.Y);
            await ClickAsync(image.ClickButton, ct).ConfigureAwait(false);
            if (image.DoubleClick)
            {
                await DelayAsync(ScaleDelay(DoubleClickGapMs), ct).ConfigureAwait(false);
                await ClickAsync(image.ClickButton, ct).ConfigureAwait(false);
            }
        }
    }

    private async Task ExecuteSoundAsync(SoundCommand sound, CancellationToken ct)
    {
        try
        {
            await _soundPlayer.PlayAsync(sound.FilePath, sound.WaitForCompletion, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Warning?.Invoke(this, $"Impossible de jouer « {sound.FilePath} » : {ex.Message}");
        }
    }

    private void ExecuteMessage(MessageCommand message)
    {
        var text = TokenExpander.Expand(message.Text, _variables, _clipboard, _clock);
        var result = _messageBox.Show(message.Title, text, message.MessageKind);
        if (!string.IsNullOrEmpty(message.ResultVariableName))
        {
            _variables.Set(message.ResultVariableName, result == MessageBoxResult.Ok ? "ok" : "cancel");
        }
    }

    private async Task ExecuteTextAsync(TextCommand text, CancellationToken ct)
    {
        var content = text.ExpandTokens ? TokenExpander.Expand(text.Text, _variables, _clipboard, _clock) : text.Text;
        for (var i = 0; i < content.Length; i++)
        {
            _simulator.TypeCharacter(content[i]);
            if (i < content.Length - 1)
            {
                await DelayAsync(ScaleDelay(text.CharacterDelayMs), ct).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Résout puis saisit un secret (mot de passe) sans jamais le faire transiter par un message d'avertissement,
    /// un journal ou une variable — seul <see cref="IInputSimulator.TypeCharacter"/> le voit.
    /// </summary>
    private async Task ExecuteSecureInputAsync(SecureInputCommand secureInput, CancellationToken ct)
    {
        string? secret;
        if (secureInput.PromptAtPlayback)
        {
            if (_securePrompter is null)
            {
                Warning?.Invoke(this, "Saisie protégée : aucune invite configurée, commande ignorée.");
                return;
            }

            secret = _securePrompter.PromptForSecret("Saisie protégée", secureInput.PromptLabel);
            if (secret is null)
            {
                Warning?.Invoke(this, "Saisie protégée : annulée par l'utilisateur.");
                return;
            }
        }
        else
        {
            if (_dataProtector is null || string.IsNullOrEmpty(secureInput.ProtectedValueBase64))
            {
                Warning?.Invoke(this, "Saisie protégée : aucun secret enregistré, commande ignorée.");
                return;
            }

            try
            {
                secret = _dataProtector.Unprotect(secureInput.ProtectedValueBase64);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Jamais ex.Message : pourrait fuiter des détails du secret selon l'implémentation de IDataProtector.
                Warning?.Invoke(this, "Saisie protégée : impossible de déchiffrer le secret (macro copiée sur un autre ordinateur/compte ?).");
                return;
            }
        }

        for (var i = 0; i < secret.Length; i++)
        {
            _simulator.TypeCharacter(secret[i]);
            if (i < secret.Length - 1)
            {
                await DelayAsync(ScaleDelay(secureInput.CharacterDelayMs), ct).ConfigureAwait(false);
            }
        }
    }

    private Task ExecuteClipboardAsync(ClipboardCommand clipboard, CancellationToken ct)
    {
        switch (clipboard.Action)
        {
            case ClipboardAction.Copy:
                _clipboard.SetText(TokenExpander.Expand(clipboard.Text, _variables, _clipboard, _clock));
                break;

            case ClipboardAction.Paste:
                PressModifiers(KeyModifiers.Ctrl);
                _simulator.KeyEvent(0x56, 0x2F, false, true); // V
                _simulator.KeyEvent(0x56, 0x2F, false, false);
                ReleaseModifiers(KeyModifiers.Ctrl);
                break;

            case ClipboardAction.ReadToVariable:
                if (!string.IsNullOrEmpty(clipboard.VariableName))
                {
                    _variables.Set(clipboard.VariableName, _clipboard.GetText());
                }

                break;
        }

        return Task.CompletedTask;
    }

    private async Task ExecuteLaunchAsync(LaunchCommand launch, CancellationToken ct)
    {
        var path = TokenExpander.Expand(launch.Path, _variables, _clipboard, _clock);
        var arguments = launch.Arguments is null ? null : TokenExpander.Expand(launch.Arguments, _variables, _clipboard, _clock);
        try
        {
            var exitCode = await _launcher.LaunchAsync(
                path, arguments, launch.WorkingDirectory, shellExecute: launch.Mode == LaunchMode.ShellCommand,
                launch.WaitForExit, launch.WaitTimeoutMs, ct).ConfigureAwait(false);

            if (launch.WaitForExit && exitCode is null)
            {
                Warning?.Invoke(this, $"« {path} » : délai d'attente dépassé.");
            }
            else if (launch.WaitForExit && exitCode != 0)
            {
                Warning?.Invoke(this, $"« {path} » s'est terminé avec le code {exitCode}.");
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Warning?.Invoke(this, $"Impossible de lancer « {path} » : {ex.Message}");
        }
    }

    /// <summary>
    /// Exécute un script C# (étape 5) via <see cref="IScriptRunner"/> avec un <see cref="ScriptGlobals"/> partageant
    /// les mêmes services que le reste de la lecture (souris/clavier/fenêtre/presse-papiers/variables). Une erreur
    /// de compilation, une exception du script ou un dépassement du délai deviennent un simple avertissement de
    /// lecture (jamais un arrêt de la macro) ; seule l'annulation (arrêt d'urgence) se propage normalement.
    /// </summary>
    private async Task ExecuteScriptAsync(ScriptCommand script, CancellationToken ct)
    {
        if (_scriptRunner is null)
        {
            Warning?.Invoke(this, "Aucun moteur de script C# configuré : commande ignorée.");
            return;
        }

        var globals = new ScriptGlobals(_simulator, _windowFinder, _windowController, _clipboard, _variables, message => Warning?.Invoke(this, message), ct);
        var result = await _scriptRunner.RunAsync(script.Code, globals, script.TimeoutSeconds * 1000, ct).ConfigureAwait(false);
        if (!result.Success)
        {
            Warning?.Invoke(this, $"Script C# : {result.ErrorMessage}");
        }
    }

    private async Task ExecuteOpenUrlAsync(OpenUrlCommand url, CancellationToken ct)
    {
        var path = TokenExpander.Expand(url.Path, _variables, _clipboard, _clock);
        try
        {
            await _launcher.LaunchAsync(path, null, null, shellExecute: true, waitForExit: false, timeoutMs: 0, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Warning?.Invoke(this, $"Impossible d'ouvrir « {path} » : {ex.Message}");
        }
    }

    private Task DelayAsync(int milliseconds, CancellationToken ct) => milliseconds <= 0 ? Task.CompletedTask : _delay(milliseconds, ct);

    private int ScaleDelay(int milliseconds) =>
        _options.NoDelay ? 0 : Math.Max(0, (int)Math.Round(milliseconds / _options.SpeedMultiplier));

    /// <summary>Relâche tout ce que ce lecteur a laissé enfoncé, quelle que soit la façon dont la lecture s'est terminée.</summary>
    private void ReleaseEverything()
    {
        foreach (var button in _heldMouseButtons)
        {
            _simulator.MouseButton(button, false);
        }

        _heldMouseButtons.Clear();

        foreach (var key in _heldKeys)
        {
            _simulator.KeyEvent(key.VirtualKey, key.ScanCode, key.Extended, false);
        }

        _heldKeys.Clear();
        _heldKeyModifiers.Clear();

        foreach (var (scanCode, virtualKey) in _heldModifierRefCounts.Keys
                     .Select(sc => (sc, vk: Modifiers.Values.First(m => m.ScanCode == sc).VirtualKey))
                     .ToList())
        {
            _simulator.KeyEvent(virtualKey, scanCode, false, false);
        }

        _heldModifierRefCounts.Clear();
    }
}

using Microsoft.Extensions.Logging;
using V0XMacroRecorder.Core.Abstractions;
using V0XMacroRecorder.Core.Macros;
using V0XMacroRecorder.Core.Recording;

namespace V0XMacroRecorder.Services;

/// <summary>
/// Implémentation Win32 du bouton ENREGISTRER : décompte de démarrage, hooks bas niveau via <see cref="InputHookThread"/>,
/// traduction en commandes via <see cref="RecordingSession"/> (Core, testé indépendamment de tout ceci).
/// </summary>
public sealed class Win32InputRecorder : IInputRecorder, IDisposable
{
    private readonly InputHookThread _hooks;
    private readonly IActiveWindowProvider _activeWindowProvider;
    private readonly ISecureInputGuard _secureInputGuard;
    private readonly ILogger<Win32InputRecorder> _logger;
    private readonly object _lock = new();

    private RecordingSession? _session;
    private CancellationTokenSource? _countdownCts;
    private readonly List<MacroCommand> _recorded = [];

    public Win32InputRecorder(
        InputHookThread hooks,
        IActiveWindowProvider activeWindowProvider,
        ISecureInputGuard secureInputGuard,
        ILogger<Win32InputRecorder> logger)
    {
        _hooks = hooks;
        _activeWindowProvider = activeWindowProvider;
        _secureInputGuard = secureInputGuard;
        _logger = logger;
    }

    public bool IsRecording { get; private set; }

    public event EventHandler<MacroCommand>? CommandRecorded;

    public event EventHandler<int>? CountdownTick;

    public async Task StartAsync(RecordingOptions options, CancellationToken cancellationToken = default)
    {
        if (IsRecording)
        {
            return;
        }

        _countdownCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        try
        {
            for (var remaining = options.StartCountdownSeconds; remaining > 0; remaining--)
            {
                CountdownTick?.Invoke(this, remaining);
                await Task.Delay(1000, _countdownCts.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            return; // Annulé pendant le décompte : rien n'a encore été enregistré.
        }

        CountdownTick?.Invoke(this, 0);

        lock (_lock)
        {
            if (IsRecording)
            {
                return;
            }

            _logger.LogInformation("Début de l'enregistrement (souris={Mouse}, clavier={Keyboard}).", options.CaptureMouse, options.CaptureKeyboard);
            _recorded.Clear();
            _session = new RecordingSession(options, _activeWindowProvider.GetActiveWindow, _secureInputGuard.IsFocusedElementSensitive);
            _session.CommandProduced += OnCommandProduced;
            _hooks.MouseRaw += OnMouseRaw;
            _hooks.KeyRaw += OnKeyRaw;
            _hooks.EnableHooks();
            IsRecording = true;
        }
    }

    public IReadOnlyList<MacroCommand> Stop()
    {
        _countdownCts?.Cancel();

        lock (_lock)
        {
            if (_session is null)
            {
                return [];
            }

            _hooks.DisableHooks();
            _hooks.MouseRaw -= OnMouseRaw;
            _hooks.KeyRaw -= OnKeyRaw;

            _session.Flush(Environment.TickCount64);
            _session.CommandProduced -= OnCommandProduced;

            _logger.LogInformation("Fin de l'enregistrement : {Count} commande(s).", _recorded.Count);
            IsRecording = false;
            _session = null;
            return _recorded.ToList();
        }
    }

    /// <summary>Accumule (pour la valeur de retour de <see cref="Stop"/>) et notifie l'appelant en direct.</summary>
    private void OnCommandProduced(object? sender, MacroCommand command)
    {
        _recorded.Add(command);
        CommandRecorded?.Invoke(this, command);
    }

    private void OnMouseRaw(object? sender, RawMouseEvent e) => _session?.HandleMouse(e);

    private void OnKeyRaw(object? sender, RawKeyEvent e) => _session?.HandleKey(e);

    public void Dispose()
    {
        if (IsRecording)
        {
            Stop();
        }
    }
}

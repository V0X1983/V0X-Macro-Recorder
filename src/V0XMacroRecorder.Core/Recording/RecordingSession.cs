using V0XMacroRecorder.Core.Macros;

namespace V0XMacroRecorder.Core.Recording;

/// <summary>
/// Traduit un flux d'événements bruts (souris/clavier) en <see cref="MacroCommand"/>, en appliquant les réglages
/// d'enregistrement (fusion des mouvements, de la molette, du texte tapé, calcul des délais). Ne dépend d'aucune
/// API Windows : entièrement testable avec des événements synthétiques et une horloge fournie par l'appelant.
///
/// Principe qui évite tout défaut d'ordre entre les commandes produites : une commande n'est JAMAIS émise en
/// attendant un événement futur incertain (ex. « peut-être un second clic va suivre ») — seuls les échantillons
/// de même nature strictement consécutifs (déplacements, crans de molette, caractères tapés) sont fusionnés, et
/// la fusion est toujours finalisée de façon synchrone, juste avant l'événement différent qui la interrompt (ou à
/// <see cref="Flush"/>). Bouton et touches génèrent donc des commandes Down/Up littérales plutôt qu'un unique
/// « Clic »/« Frappe » reconstitué : le raccourci est un peu plus verbeux mais ne peut jamais placer une commande
/// au mauvais endroit de la liste (ce qu'un « clic » reconstitué après coup, une fois le relâchement connu,
/// pourrait faire si une touche ou un autre bouton a été actionné entre-temps).
/// </summary>
public sealed class RecordingSession
{
    private const int WheelMergeWindowMs = 150;

    private const int TextMergeTimeoutMs = 2000;

    private static readonly HashSet<int> ModifierVirtualKeys = [0x10, 0x11, 0x12, 0xA0, 0xA1, 0xA2, 0xA3, 0xA4, 0xA5, 0x5B, 0x5C];

    private readonly RecordingOptions _options;
    private readonly Func<ActiveWindowInfo?> _activeWindowProvider;
    private readonly Func<bool> _isSensitiveFieldFocused;

    private long? _lastEmittedAtMs;
    private int? _lastAbsoluteX;
    private int? _lastAbsoluteY;

    private (int X, int Y, long FirstAtMs)? _pendingMove;
    private (int TotalDelta, int X, int Y, long FirstAtMs, long LastAtMs)? _pendingWheel;

    private readonly HashSet<int> _heldModifiers = [];
    private readonly HashSet<int> _modifiersUsedInChord = [];
    private readonly HashSet<int> _heldRegularKeys = [];
    private readonly HashSet<int> _pendingTextKeys = [];
    private readonly List<char> _textBuffer = [];
    private long _textBufferStartAtMs;
    private long _lastTextCharAtMs;

    public RecordingSession(
        RecordingOptions options,
        Func<ActiveWindowInfo?>? activeWindowProvider = null,
        Func<bool>? isSensitiveFieldFocused = null)
    {
        _options = options;
        _activeWindowProvider = activeWindowProvider ?? (() => null);
        _isSensitiveFieldFocused = isSensitiveFieldFocused ?? (() => false);
    }

    /// <summary>Une commande vient d'être décidée ; son <see cref="MacroCommand.DelayMs"/> est déjà calculé.</summary>
    public event EventHandler<MacroCommand>? CommandProduced;

    public void HandleMouse(RawMouseEvent e)
    {
        if (e.Injected || !_options.CaptureMouse)
        {
            return;
        }

        FlushTextBuffer();
        ExpireStaleState(e.TimestampMs);

        switch (e.Kind)
        {
            case RawMouseKind.Move:
                HandleMove(e);
                break;
            case RawMouseKind.Down:
                HandleButton(e, MouseAction.Down);
                break;
            case RawMouseKind.Up:
                HandleButton(e, MouseAction.Up);
                break;
            case RawMouseKind.Wheel:
                HandleWheel(e);
                break;
        }
    }

    public void HandleKey(RawKeyEvent e)
    {
        if (e.Injected || !_options.CaptureKeyboard)
        {
            return;
        }

        if (_options.DontRecordPasswordFields && _isSensitiveFieldFocused())
        {
            // On efface tout état en cours plutôt que de risquer de faire fuiter un fragment de mot de passe.
            _heldRegularKeys.Clear();
            _pendingTextKeys.Clear();
            _textBuffer.Clear();
            return;
        }

        FlushMove();
        FlushWheel();
        ExpireStaleState(e.TimestampMs);

        if (ModifierVirtualKeys.Contains(e.VirtualKey))
        {
            HandleModifierKey(e);
        }
        else
        {
            HandleRegularKey(e);
        }
    }

    /// <summary>À appeler à l'arrêt de l'enregistrement : finalise l'état en attente (déplacement, molette, texte).</summary>
    public void Flush(long nowMs)
    {
        ExpireStaleState(nowMs, force: true);
        FlushMove();
        _heldRegularKeys.Clear();
        _pendingTextKeys.Clear();
        _heldModifiers.Clear();
        _modifiersUsedInChord.Clear();
    }

    // ------------------------------------------------------------------------------------------------ Souris

    private void HandleMove(RawMouseEvent e)
    {
        FlushWheel();

        if (_options.MouseSampling == MouseSamplingMode.FullPath)
        {
            _pendingMove = (e.ScreenX, e.ScreenY, e.TimestampMs);
            FlushMove();
            return;
        }

        // ClicksAndEndpoints : les échantillons consécutifs fusionnent en un seul segment (position finale gardée,
        // délai ancré sur le début du segment).
        _pendingMove = _pendingMove is { } pending
            ? (e.ScreenX, e.ScreenY, pending.FirstAtMs)
            : (e.ScreenX, e.ScreenY, e.TimestampMs);
    }

    private void HandleButton(RawMouseEvent e, MouseAction action)
    {
        FlushMove(suppressIfEquals: (e.ScreenX, e.ScreenY));
        FlushWheel();
        Emit(BuildMouseCommand(action, e.Button, e.ScreenX, e.ScreenY, 0), e.TimestampMs);
    }

    private void HandleWheel(RawMouseEvent e)
    {
        FlushMove(suppressIfEquals: (e.ScreenX, e.ScreenY));

        if (_pendingWheel is { } pending
            && Math.Sign(pending.TotalDelta) == Math.Sign(e.WheelDelta)
            && e.TimestampMs - pending.LastAtMs <= WheelMergeWindowMs)
        {
            _pendingWheel = (pending.TotalDelta + e.WheelDelta, e.ScreenX, e.ScreenY, pending.FirstAtMs, e.TimestampMs);
            return;
        }

        FlushWheel();
        _pendingWheel = (e.WheelDelta, e.ScreenX, e.ScreenY, e.TimestampMs, e.TimestampMs);
    }

    /// <summary>Émet la commande Déplacer en attente, sauf si sa position finale est déjà celle de l'événement qui suit (redondant).</summary>
    private void FlushMove((int X, int Y)? suppressIfEquals = null)
    {
        if (_pendingMove is not { } pending)
        {
            return;
        }

        _pendingMove = null;
        if (suppressIfEquals is { } target && target.X == pending.X && target.Y == pending.Y)
        {
            return;
        }

        Emit(BuildMouseCommand(MouseAction.Move, MouseButton.Left, pending.X, pending.Y, 0), pending.FirstAtMs);
    }

    private void FlushWheel()
    {
        if (_pendingWheel is not { } pending)
        {
            return;
        }

        _pendingWheel = null;
        Emit(BuildMouseCommand(MouseAction.Wheel, MouseButton.Left, pending.X, pending.Y, pending.TotalDelta), pending.FirstAtMs);
    }

    /// <summary>Convertit une position écran brute selon le repère de coordonnées choisi et met à jour le repère « Relatif ».</summary>
    private MouseCommand BuildMouseCommand(MouseAction action, MouseButton button, int screenX, int screenY, int wheelDelta)
    {
        var previousX = _lastAbsoluteX;
        var previousY = _lastAbsoluteY;
        _lastAbsoluteX = screenX;
        _lastAbsoluteY = screenY;

        switch (_options.CoordinateMode)
        {
            case CoordinateMode.ActiveWindow:
                var window = _activeWindowProvider();
                if (window is not null)
                {
                    return new MouseCommand
                    {
                        Action = action,
                        Button = button,
                        WheelDelta = wheelDelta,
                        CoordinateMode = CoordinateMode.ActiveWindow,
                        X = screenX - window.Left,
                        Y = screenY - window.Top,
                        WindowTitle = window.Title,
                        WindowClassName = window.ClassName,
                    };
                }

                goto case CoordinateMode.Screen; // Pas de fenêtre active résolue : repli sur l'écran.

            case CoordinateMode.Relative:
                return new MouseCommand
                {
                    Action = action,
                    Button = button,
                    WheelDelta = wheelDelta,
                    CoordinateMode = CoordinateMode.Relative,
                    X = previousX is { } px ? screenX - px : 0,
                    Y = previousY is { } py ? screenY - py : 0,
                };

            case CoordinateMode.Screen:
            default:
                return new MouseCommand
                {
                    Action = action,
                    Button = button,
                    WheelDelta = wheelDelta,
                    CoordinateMode = CoordinateMode.Screen,
                    X = screenX,
                    Y = screenY,
                };
        }
    }

    // ------------------------------------------------------------------------------------------------ Clavier

    private void HandleModifierKey(RawKeyEvent e)
    {
        var vk = e.VirtualKey;
        if (e.IsKeyDown)
        {
            if (_heldModifiers.Add(vk))
            {
                _modifiersUsedInChord.Remove(vk);
            }
        }
        else if (_heldModifiers.Remove(vk) && !_modifiersUsedInChord.Remove(vk))
        {
            // Relâchée seule, jamais combinée à une autre touche (ex. Alt seul ouvre le menu, Win seul le menu Démarrer).
            // Sans risque d'ordre : si une autre touche avait été pressée entre-temps, elle aurait marqué le
            // modificateur « utilisé », et ce cas ne se produirait pas.
            Emit(new KeyboardCommand { VirtualKey = vk, ScanCode = e.ScanCode, IsExtendedKey = e.IsExtended, Action = KeyAction.Press }, e.TimestampMs);
        }
    }

    private void HandleRegularKey(RawKeyEvent e)
    {
        var vk = e.VirtualKey;
        if (e.IsKeyDown)
        {
            if (!_heldRegularKeys.Add(vk))
            {
                return; // Répétition automatique du système pendant le maintien : ignorée.
            }

            foreach (var modifier in _heldModifiers)
            {
                _modifiersUsedInChord.Add(modifier);
            }

            var modifiers = CurrentModifiers();
            var isTextEligible = _options.RecordTypedTextAsString
                                  && e.Character is { } ch
                                  && !char.IsControl(ch)
                                  && modifiers is KeyModifiers.None or KeyModifiers.Shift;

            if (isTextEligible)
            {
                if (_textBuffer.Count == 0)
                {
                    _textBufferStartAtMs = e.TimestampMs;
                }

                _textBuffer.Add(e.Character!.Value);
                _lastTextCharAtMs = e.TimestampMs;
                _pendingTextKeys.Add(vk);
            }
            else
            {
                FlushTextBuffer();
                Emit(new KeyboardCommand { VirtualKey = vk, ScanCode = e.ScanCode, IsExtendedKey = e.IsExtended, Action = KeyAction.Down, Modifiers = modifiers }, e.TimestampMs);
            }
        }
        else
        {
            _heldRegularKeys.Remove(vk);
            if (_pendingTextKeys.Remove(vk))
            {
                return; // Relâchement silencieux : déjà représentée dans la commande Texte.
            }

            Emit(new KeyboardCommand { VirtualKey = vk, ScanCode = e.ScanCode, IsExtendedKey = e.IsExtended, Action = KeyAction.Up, Modifiers = CurrentModifiers() }, e.TimestampMs);
        }
    }

    private KeyModifiers CurrentModifiers()
    {
        var result = KeyModifiers.None;
        foreach (var vk in _heldModifiers)
        {
            result |= vk switch
            {
                0x10 or 0xA0 or 0xA1 => KeyModifiers.Shift,
                0x11 or 0xA2 or 0xA3 => KeyModifiers.Ctrl,
                0x12 or 0xA4 or 0xA5 => KeyModifiers.Alt,
                0x5B or 0x5C => KeyModifiers.Win,
                _ => KeyModifiers.None,
            };
        }

        return result;
    }

    private void FlushTextBuffer()
    {
        if (_textBuffer.Count == 0)
        {
            return;
        }

        var text = new string(_textBuffer.ToArray());
        _textBuffer.Clear();
        Emit(new TextCommand { Text = text }, _textBufferStartAtMs);
    }

    // ------------------------------------------------------------------------------------------------ Commun

    /// <summary>Finalise l'état qui dépend du temps écoulé (fusion molette, fusion texte), indépendamment du type du prochain événement.</summary>
    private void ExpireStaleState(long nowMs, bool force = false)
    {
        if (_pendingWheel is { } wheel && (force || nowMs - wheel.LastAtMs > WheelMergeWindowMs))
        {
            FlushWheel();
        }

        if (_textBuffer.Count > 0 && (force || nowMs - _lastTextCharAtMs > TextMergeTimeoutMs))
        {
            FlushTextBuffer();
        }
    }

    private void Emit(MacroCommand command, long atMs)
    {
        var delay = 0;
        if (_options.RecordDelays)
        {
            delay = _lastEmittedAtMs is { } last ? (int)Math.Clamp(atMs - last, 0, int.MaxValue) : 0;
            if (_options.DelayCapMs is { } cap)
            {
                delay = Math.Min(delay, cap);
            }
        }

        command.DelayMs = delay;
        _lastEmittedAtMs = atMs;
        CommandProduced?.Invoke(this, command);
    }
}

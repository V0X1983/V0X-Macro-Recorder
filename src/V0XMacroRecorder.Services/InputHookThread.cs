using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using V0XMacroRecorder.Core.Abstractions;
using V0XMacroRecorder.Core.Macros;
using V0XMacroRecorder.Core.Recording;
using V0XMacroRecorder.Services.Native;

namespace V0XMacroRecorder.Services;

/// <summary>
/// Thread dédié unique portant à la fois les hooks bas niveau (WH_MOUSE_LL / WH_KEYBOARD_LL) et le raccourci
/// global (RegisterHotKey), avec une pompe de messages minimale (aucune fenêtre réelle : RegisterHotKey(hWnd=NULL)
/// poste WM_HOTKEY à la file du thread appelant). Les callbacks de hook ne font qu'un examen très court des
/// structures reçues et mettent les événements en file (<see cref="BlockingCollection{T}"/>) ; le traitement
/// réel (résolution du caractère, callbacks publics) a lieu sur un thread consommateur séparé, jamais dans le
/// callback lui-même (Windows retire silencieusement un hook trop lent à répondre).
/// </summary>
public sealed class InputHookThread : IGlobalHotKeyService, IDisposable
{
    private readonly Thread _messageThread;
    private readonly ManualResetEventSlim _threadReady = new(false);
    private readonly BlockingCollection<Action> _rawEventQueue = new();
    private readonly CancellationTokenSource _consumerCts = new();
    private readonly Task _consumerTask;
    private readonly KeyCharacterResolver _keyCharacters = new();

    // Racines des délégués natifs : sans ce champ, le GC pourrait les collecter alors que Windows les appelle encore.
    private readonly NativeMethods.HookProc _mouseHookProc;
    private readonly NativeMethods.HookProc _keyboardHookProc;

    private uint _threadId;
    private IntPtr _mouseHook;
    private IntPtr _keyboardHook;
    private bool _disposed;

    public InputHookThread()
    {
        _mouseHookProc = MouseHookProc;
        _keyboardHookProc = KeyboardHookProc;

        _messageThread = new Thread(RunMessageLoop) { IsBackground = true, Name = "V0XMacroRecorder.InputHook" };
        _messageThread.Start();
        _threadReady.Wait();

        _consumerTask = Task.Factory.StartNew(ConsumeQueue, TaskCreationOptions.LongRunning);
    }

    public event EventHandler<RawMouseEvent>? MouseRaw;

    public event EventHandler<RawKeyEvent>? KeyRaw;

    public event EventHandler<int>? HotKeyPressed;

    public void EnableHooks() => InvokeOnThread(() =>
    {
        _mouseHook = _mouseHook != IntPtr.Zero ? _mouseHook
            : NativeMethods.SetWindowsHookExW(NativeMethods.WH_MOUSE_LL, _mouseHookProc, NativeMethods.GetModuleHandleW(null), 0);
        _keyboardHook = _keyboardHook != IntPtr.Zero ? _keyboardHook
            : NativeMethods.SetWindowsHookExW(NativeMethods.WH_KEYBOARD_LL, _keyboardHookProc, NativeMethods.GetModuleHandleW(null), 0);
    });

    public void DisableHooks() => InvokeOnThread(() =>
    {
        if (_mouseHook != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_mouseHook);
            _mouseHook = IntPtr.Zero;
        }

        if (_keyboardHook != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_keyboardHook);
            _keyboardHook = IntPtr.Zero;
        }
    });

    public bool TryRegister(int id, KeyModifiers modifiers, int virtualKey)
    {
        var registered = false;
        InvokeOnThread(() =>
        {
            uint mod = NativeMethods.MOD_NOREPEAT;
            if (modifiers.HasFlag(KeyModifiers.Alt)) mod |= NativeMethods.MOD_ALT;
            if (modifiers.HasFlag(KeyModifiers.Ctrl)) mod |= NativeMethods.MOD_CONTROL;
            if (modifiers.HasFlag(KeyModifiers.Shift)) mod |= NativeMethods.MOD_SHIFT;
            if (modifiers.HasFlag(KeyModifiers.Win)) mod |= NativeMethods.MOD_WIN;
            registered = NativeMethods.RegisterHotKey(IntPtr.Zero, id, mod, (uint)virtualKey);
        });
        return registered;
    }

    public void Unregister(int id) => InvokeOnThread(() => NativeMethods.UnregisterHotKey(IntPtr.Zero, id));

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            DisableHooks();
        }
        catch (Exception ex) when (ex is InvalidOperationException or SEHException)
        {
            // Le thread de la pompe a peut-être déjà quitté : rien de plus à faire.
        }

        _consumerCts.Cancel();
        _rawEventQueue.CompleteAdding();
        NativeMethods.PostThreadMessageW(_threadId, NativeMethods.WM_QUIT, IntPtr.Zero, IntPtr.Zero);
        _messageThread.Join(TimeSpan.FromSeconds(2));
        _consumerCts.Dispose();
        _threadReady.Dispose();
        _rawEventQueue.Dispose();
    }

    // -------------------------------------------------------------------------------------- Pompe de messages

    private void RunMessageLoop()
    {
        _threadId = NativeMethods.GetCurrentThreadId();
        _threadReady.Set();

        while (NativeMethods.GetMessageW(out var msg, IntPtr.Zero, 0, 0))
        {
            if (msg.message == NativeMethods.WM_HOTKEY)
            {
                HotKeyPressed?.Invoke(this, (int)msg.wParam);
            }
            else if (msg.message == NativeMethods.WM_APP_INVOKE && msg.lParam != IntPtr.Zero)
            {
                var handle = GCHandle.FromIntPtr(msg.lParam);
                var action = (Action)handle.Target!;
                handle.Free();
                action();
            }
        }
    }

    /// <summary>Exécute <paramref name="action"/> sur le thread de la pompe et attend son achèvement (SetWindowsHookEx/RegisterHotKey doivent s'exécuter depuis ce thread précis).</summary>
    private void InvokeOnThread(Action action)
    {
        if (NativeMethods.GetCurrentThreadId() == _threadId)
        {
            action();
            return;
        }

        using var done = new ManualResetEventSlim(false);
        Exception? error = null;
        var handle = GCHandle.Alloc(new Action(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                error = ex;
            }
            finally
            {
                done.Set();
            }
        }));

        NativeMethods.PostThreadMessageW(_threadId, NativeMethods.WM_APP_INVOKE, IntPtr.Zero, GCHandle.ToIntPtr(handle));
        done.Wait();
        if (error is not null)
        {
            throw new InvalidOperationException("Échec d'une opération sur le thread des périphériques d'entrée.", error);
        }
    }

    // -------------------------------------------------------------------------------------- Callbacks de hook (très courts)

    private IntPtr MouseHookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var data = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
            var injected = (data.flags & NativeMethods.LLMHF_INJECTED) != 0;
            var message = (int)wParam;

            RawMouseKind? kind = message switch
            {
                NativeMethods.WM_MOUSEMOVE => RawMouseKind.Move,
                NativeMethods.WM_LBUTTONDOWN or NativeMethods.WM_RBUTTONDOWN or NativeMethods.WM_MBUTTONDOWN => RawMouseKind.Down,
                NativeMethods.WM_LBUTTONUP or NativeMethods.WM_RBUTTONUP or NativeMethods.WM_MBUTTONUP => RawMouseKind.Up,
                NativeMethods.WM_MOUSEWHEEL => RawMouseKind.Wheel,
                _ => null,
            };

            if (kind is { } k)
            {
                var button = message switch
                {
                    NativeMethods.WM_RBUTTONDOWN or NativeMethods.WM_RBUTTONUP => MouseButton.Right,
                    NativeMethods.WM_MBUTTONDOWN or NativeMethods.WM_MBUTTONUP => MouseButton.Middle,
                    _ => MouseButton.Left,
                };

                var wheelDelta = 0;
                if (k == RawMouseKind.Wheel)
                {
                    var raw = unchecked((short)((data.mouseData >> 16) & 0xFFFF));
                    wheelDelta = Math.Sign(raw) * Math.Max(1, Math.Abs(raw) / 120);
                }

                var ev = new RawMouseEvent(k, button, data.pt.X, data.pt.Y, wheelDelta, data.time, injected);
                _rawEventQueue.Add(() => MouseRaw?.Invoke(this, ev));
            }
        }

        return NativeMethods.CallNextHookEx(_mouseHook, nCode, wParam, lParam);
    }

    private IntPtr KeyboardHookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var data = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);
            var injected = (data.flags & NativeMethods.LLKHF_INJECTED) != 0;
            var isExtended = (data.flags & NativeMethods.LLKHF_EXTENDED) != 0;
            var message = (int)wParam;

            bool? isDown = message switch
            {
                NativeMethods.WM_KEYDOWN or NativeMethods.WM_SYSKEYDOWN => true,
                NativeMethods.WM_KEYUP or NativeMethods.WM_SYSKEYUP => false,
                _ => null,
            };

            if (isDown is { } down)
            {
                var virtualKey = (int)data.vkCode;
                var scanCode = (int)data.scanCode;
                var time = data.time;
                _rawEventQueue.Add(() =>
                {
                    var character = down && !injected ? _keyCharacters.TryResolve(virtualKey, scanCode, isExtended) : null;
                    var ev = new RawKeyEvent(virtualKey, scanCode, isExtended, down, time, injected, character);
                    KeyRaw?.Invoke(this, ev);
                });
            }
        }

        return NativeMethods.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
    }

    private void ConsumeQueue()
    {
        try
        {
            foreach (var action in _rawEventQueue.GetConsumingEnumerable(_consumerCts.Token))
            {
                action();
            }
        }
        catch (OperationCanceledException)
        {
            // Arrêt normal (Dispose).
        }
    }
}

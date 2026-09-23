using V0XMacroRecorder.Core.Abstractions;
using V0XMacroRecorder.Core.Recording;

namespace V0XMacroRecorder.Services;

/// <summary>
/// Attend une frappe clavier réelle (non injectée) via le hook bas niveau déjà porté par <see cref="InputHookThread"/>
/// (le même thread/hook que l'enregistrement, réutilisé plutôt que dupliqué). Activé/désactivé le temps de l'attente
/// seulement : n'impose pas le coût d'un hook permanent en dehors des commandes Attente/Pause qui en ont besoin.
/// </summary>
public sealed class Win32KeyWaiter(InputHookThread hooks) : IKeyWaiter
{
    public async Task<bool> WaitForKeyAsync(int virtualKey, int timeoutMs, CancellationToken ct)
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        void OnKeyRaw(object? sender, RawKeyEvent e)
        {
            if (e.Injected || !e.IsKeyDown)
            {
                return;
            }

            if (virtualKey != 0 && e.VirtualKey != virtualKey)
            {
                return;
            }

            tcs.TrySetResult(true);
        }

        hooks.EnableHooks();
        hooks.KeyRaw += OnKeyRaw;

        using var timer = timeoutMs > 0 ? new Timer(_ => tcs.TrySetResult(false), null, timeoutMs, Timeout.Infinite) : null;
        await using var ctr = ct.Register(() => tcs.TrySetCanceled(ct)).ConfigureAwait(false);

        try
        {
            return await tcs.Task.ConfigureAwait(false);
        }
        finally
        {
            hooks.KeyRaw -= OnKeyRaw;
            hooks.DisableHooks();
        }
    }
}

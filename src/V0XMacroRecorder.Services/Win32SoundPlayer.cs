using System.Media;
using V0XMacroRecorder.Core.Abstractions;

namespace V0XMacroRecorder.Services;

/// <summary>Lecture de fichiers .wav via <see cref="SoundPlayer"/> (simple, suffisant pour un signal sonore de macro).</summary>
public sealed class Win32SoundPlayer : ISoundPlayer
{
    public async Task PlayAsync(string filePath, bool waitForCompletion, CancellationToken ct)
    {
        if (waitForCompletion)
        {
            using var player = new SoundPlayer(filePath);
            await Task.Run(player.PlaySync, ct).ConfigureAwait(false);
            return;
        }

        // Lecture non bloquante : ne pas disposer tout de suite, sinon la lecture s'interrompt (Play() est déjà asynchrone en interne).
        new SoundPlayer(filePath).Play();
    }
}

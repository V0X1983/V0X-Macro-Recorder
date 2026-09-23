using System.Windows;
using V0XMacroRecorder.App.Views;
using V0XMacroRecorder.Core.Abstractions;

namespace V0XMacroRecorder.App.Infrastructure;

/// <summary>Affiche <see cref="SecureInputPromptWindow"/>, toujours sur le thread UI (comme <c>Win32MessageBoxService</c> : <see cref="Core.Playback.MacroPlayer"/> tourne en tâche de fond).</summary>
public sealed class SecureInputPrompter : ISecureInputPrompter
{
    public string? PromptForSecret(string title, string message)
    {
        var dispatcher = Application.Current?.Dispatcher;
        Func<string?> show = () =>
        {
            var window = new SecureInputPromptWindow(title, message) { Owner = Application.Current?.MainWindow };
            return window.ShowDialog() == true ? window.Secret : null;
        };

        return dispatcher is null || dispatcher.CheckAccess() ? show() : dispatcher.Invoke(show);
    }
}

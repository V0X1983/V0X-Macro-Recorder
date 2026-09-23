using System.Windows;
using CoreAbstractions = V0XMacroRecorder.Core.Abstractions;
using V0XMacroRecorder.Core.Macros;

namespace V0XMacroRecorder.Services;

/// <summary>Boîte de message WPF, toujours affichée sur le thread UI (MacroPlayer tourne en tâche de fond).</summary>
public sealed class Win32MessageBoxService : CoreAbstractions.IMessageBoxService
{
    public CoreAbstractions.MessageBoxResult Show(string title, string text, MessageBoxKind kind)
    {
        var image = kind switch
        {
            MessageBoxKind.Warning => MessageBoxImage.Warning,
            MessageBoxKind.Error => MessageBoxImage.Error,
            _ => MessageBoxImage.Information,
        };
        var button = kind == MessageBoxKind.OkCancel ? MessageBoxButton.OKCancel : MessageBoxButton.OK;

        var dispatcher = Application.Current?.Dispatcher;
        Func<CoreAbstractions.MessageBoxResult> show = () =>
            MessageBox.Show(text, title, button, image) == MessageBoxResult.Cancel
                ? CoreAbstractions.MessageBoxResult.Cancel
                : CoreAbstractions.MessageBoxResult.Ok;

        return dispatcher is null || dispatcher.CheckAccess() ? show() : dispatcher.Invoke(show);
    }
}

using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace V0XMacroRecorder.App.Infrastructure;

/// <summary>
/// Remplace <c>System.Windows.Application.Current.MainWindow</c>/<c>.Dispatcher</c> (WPF) : WinUI 3 n'a pas de
/// notion intégrée de "fenêtre principale" ni de <c>Window.Owner</c>. <see cref="MainWindow"/> est renseignée une
/// fois par <c>App.xaml.cs</c> juste après sa création, avant que les ViewModels qui en dépendent ne soient utilisés.
/// </summary>
public interface IAppWindowProvider
{
    Window MainWindow { get; }

    DispatcherQueue DispatcherQueue { get; }
}

public sealed class AppWindowProvider : IAppWindowProvider
{
    private Window? _mainWindow;

    public Window MainWindow => _mainWindow ?? throw new InvalidOperationException(
        "MainWindow n'a pas encore été assignée (voir App.xaml.cs.OnLaunched).");

    public DispatcherQueue DispatcherQueue { get; } = DispatcherQueue.GetForCurrentThread()
        ?? throw new InvalidOperationException("AppWindowProvider doit être construit sur le thread UI.");

    public void SetMainWindow(Window window) => _mainWindow = window;
}

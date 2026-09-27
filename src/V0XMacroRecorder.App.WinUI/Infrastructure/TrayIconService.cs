using System.IO;
using CommunityToolkit.Mvvm.Input;
using H.NotifyIcon;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using V0XMacroRecorder.App.ViewModels;
using V0XMacroRecorder.Core.Abstractions;

namespace V0XMacroRecorder.App.Infrastructure;

/// <summary>
/// Icône de zone de notification, via <c>H.NotifyIcon.WinUI</c> (validé au spike de faisabilité : fonctionne tel
/// quel en contexte packagé MSIX — voir le paquet WinForms <c>NotifyIcon</c> utilisé côté WPF, qui n'a pas
/// d'équivalent natif WinUI 3). Toujours visible tant que l'application tourne, comme côté WPF.
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private readonly IAppWindowProvider _appWindow;
    private readonly MacroDocumentViewModel _document;
    private readonly ISettingsService _settings;
    private readonly BackgroundMacroRunner _runner;
    private readonly TaskbarIcon _trayIcon;
    private readonly MenuFlyoutItem _recordItem;
    private readonly MenuFlyoutItem _stopItem;
    private readonly MenuFlyoutSubItem _recentMacrosItem;

    public TrayIconService(IAppWindowProvider appWindow, MacroDocumentViewModel document, ISettingsService settings, BackgroundMacroRunner runner)
    {
        _appWindow = appWindow;
        _document = document;
        _settings = settings;
        _runner = runner;

        _recordItem = new MenuFlyoutItem { Text = "Enregistrer" };
        _recordItem.Click += (_, _) => _document.ToggleRecordingCommand.Execute(null);

        _stopItem = new MenuFlyoutItem { Text = "Arrêter la lecture" };
        _stopItem.Click += (_, _) => StopEverything();

        _recentMacrosItem = new MenuFlyoutSubItem { Text = "Lancer une macro récente" };

        var openItem = new MenuFlyoutItem { Text = "Ouvrir V0X Macro Recorder" };
        openItem.Click += (_, _) => ShowMainWindow();

        var quitItem = new MenuFlyoutItem { Text = "Quitter" };
        quitItem.Click += (_, _) => (_appWindow.MainWindow as MainWindow)?.ExitForReal();

        var menu = new MenuFlyout();
        menu.Items.Add(openItem);
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(_recordItem);
        menu.Items.Add(_stopItem);
        menu.Items.Add(_recentMacrosItem);
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(quitItem);
        menu.Opening += (_, _) => RefreshMenu();

        _trayIcon = new TaskbarIcon
        {
            IconSource = new BitmapImage(new Uri("ms-appx:///Assets/AppIcon.ico")),
            ToolTipText = "V0X Macro Recorder",
            ContextFlyout = menu,
            DoubleClickCommand = new RelayCommand(ShowMainWindow),
        };
        _trayIcon.ForceCreate();
    }

    private void RefreshMenu()
    {
        _recordItem.Text = _document.IsRecording ? "Arrêter l'enregistrement" : "Enregistrer";
        _recordItem.IsEnabled = !_document.IsPlaying;
        _stopItem.IsEnabled = _document.IsPlaying || _runner.IsRunning;

        _recentMacrosItem.Items.Clear();
        foreach (var path in _settings.Current.RecentFiles)
        {
            var item = new MenuFlyoutItem { Text = Path.GetFileName(path) };
            item.Click += (_, _) => _ = _runner.RunAsync(path);
            _recentMacrosItem.Items.Add(item);
        }

        _recentMacrosItem.IsEnabled = _recentMacrosItem.Items.Count > 0;
    }

    private void StopEverything()
    {
        if (_document.IsPlaying)
        {
            _document.TogglePlaybackCommand.Execute(null);
        }

        _runner.Stop();
    }

    private void ShowMainWindow()
    {
        var window = _appWindow.MainWindow;
        window.AppWindow.Show(); // Annule un Hide() précédent (réduction dans la zone de notification, voir MainWindow.OnAppWindowClosing).
        if (window.AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
        {
            presenter.Restore(); // Annule aussi une minimisation classique (barre des tâches), pas seulement le Hide() ci-dessus.
        }

        window.Activate();
    }

    public void Dispose() => _trayIcon.Dispose();
}

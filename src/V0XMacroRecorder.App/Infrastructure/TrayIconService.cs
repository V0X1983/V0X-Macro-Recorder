using System.IO;
using System.Windows;
using System.Windows.Forms;
using V0XMacroRecorder.App.ViewModels;
using V0XMacroRecorder.Core.Abstractions;
using Application = System.Windows.Application;

namespace V0XMacroRecorder.App.Infrastructure;

/// <summary>
/// Icône de zone de notification (étape 6) : toujours visible tant que l'application tourne (contrairement à
/// V0X Cleaner où elle dépend d'un réglage), puisqu'elle sert ici à relancer des macros et pas seulement à
/// surveiller. WPF n'a pas d'API native pour une icône système : <see cref="NotifyIcon"/> (interop WinForms),
/// approche standard pour ce besoin en WPF.
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private readonly MainWindow _mainWindow;
    private readonly MacroDocumentViewModel _document;
    private readonly ISettingsService _settings;
    private readonly BackgroundMacroRunner _runner;
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _recordItem;
    private readonly ToolStripMenuItem _stopItem;
    private readonly ToolStripMenuItem _recentMacrosItem;

    public TrayIconService(MainWindow mainWindow, MacroDocumentViewModel document, ISettingsService settings, BackgroundMacroRunner runner)
    {
        _mainWindow = mainWindow;
        _document = document;
        _settings = settings;
        _runner = runner;

        _recordItem = new ToolStripMenuItem("Enregistrer", null, (_, _) => _document.ToggleRecordingCommand.Execute(null));
        _stopItem = new ToolStripMenuItem("Arrêter la lecture", null, (_, _) => StopEverything());
        _recentMacrosItem = new ToolStripMenuItem("Lancer une macro récente");

        var menu = new ContextMenuStrip();
        menu.Items.Add("Ouvrir V0X Macro Recorder", null, (_, _) => ShowMainWindow());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_recordItem);
        menu.Items.Add(_stopItem);
        menu.Items.Add(_recentMacrosItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Quitter", null, (_, _) => _mainWindow.ExitForReal());
        menu.Opening += (_, _) => RefreshMenu();

        _notifyIcon = new NotifyIcon
        {
            Icon = LoadIcon(),
            Text = "V0X Macro Recorder",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _notifyIcon.DoubleClick += (_, _) => ShowMainWindow();
    }

    private static System.Drawing.Icon LoadIcon()
    {
        var stream = Application.GetResourceStream(new Uri("pack://application:,,,/Resources/app.ico"))?.Stream;
        return stream is null ? System.Drawing.SystemIcons.Application : new System.Drawing.Icon(stream);
    }

    private void RefreshMenu()
    {
        _recordItem.Text = _document.IsRecording ? "Arrêter l'enregistrement" : "Enregistrer";
        _recordItem.Enabled = !_document.IsPlaying;
        _stopItem.Enabled = _document.IsPlaying || _runner.IsRunning;

        _recentMacrosItem.DropDownItems.Clear();
        foreach (var path in _settings.Current.RecentFiles)
        {
            _recentMacrosItem.DropDownItems.Add(Path.GetFileName(path), null, (_, _) => _ = _runner.RunAsync(path));
        }

        _recentMacrosItem.Enabled = _recentMacrosItem.DropDownItems.Count > 0;
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
        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }

    public void Dispose() => _notifyIcon.Dispose();
}

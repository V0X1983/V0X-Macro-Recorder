using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;
using Serilog;
using V0XMacroRecorder.App.Helpers;
using V0XMacroRecorder.App.Infrastructure;
using V0XMacroRecorder.App.ViewModels;
using V0XMacroRecorder.Core.Abstractions;
using V0XMacroRecorder.Core.CommandLine;
using V0XMacroRecorder.Core.Macros;
using V0XMacroRecorder.Core.Models;
using V0XMacroRecorder.Services;
using V0XMacroRecorder.Services.Native;

namespace V0XMacroRecorder.App;

/// <summary>
/// Point d'entrée WinUI 3. Reprend le même modèle Generic Host + Serilog que l'ancienne app WPF
/// (<c>V0XMacroRecorder.App</c>) : un seul host applicatif, les services métier enregistrés une fois via
/// <see cref="ServiceCollectionExtensions.AddV0XMacroRecorderServices"/>.
/// </summary>
public partial class App : Application
{
    private IHost? _host;
    private Window? _window;

    public App()
    {
        InitializeComponent();
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                Path.Combine(AppPaths.LogsFolder, "v0xmacrorecorder-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14)
            .CreateLogger();

        _host = Host.CreateDefaultBuilder()
            .UseSerilog()
            .ConfigureServices((_, services) =>
            {
                services.AddV0XMacroRecorderServices();

                services.AddSingleton<IAppWindowProvider, AppWindowProvider>();
                services.AddSingleton<IDialogService, DialogService>();
                services.AddSingleton<ISecureInputPrompter, SecureInputPrompter>();
                services.AddSingleton<MacroDocumentViewModel>();
                services.AddSingleton<RecordingOptionsViewModel>();
                services.AddSingleton<PlaybackOptionsViewModel>();
                services.AddSingleton<MainWindowViewModel>();
                services.AddSingleton<BackgroundMacroRunner>();
                services.AddSingleton<MacroHotkeyManager>();
                services.AddSingleton<TrayIconService>();
            })
            .Build();

        RegisterGlobalExceptionHandlers();
        UnhandledException += (_, args) =>
        {
            Log.Fatal(args.Exception, "Exception non gérée (UI).");
            args.Handled = true; // Comme DispatcherUnhandledException côté WPF : on tente de garder l'application vivante plutôt que de la laisser planter silencieusement.
        };

        _host.Start();

        // Ligne de commande --play "chemin.v0xmacro" [--silent] [--repeat N] (typiquement une tâche planifiée).
        // Repris tel quel de l'ancienne app WPF : Environment.GetCommandLineArgs() fonctionne à l'identique en
        // WinUI 3/Windows App SDK pour un lancement normal ou en debug packagé. Association de fichier .v0xmacro
        // (double-clic dans l'Explorateur) pas encore faite : app non empaquetée (pas de manifeste MSIX), nécessiterait
        // une clé de registre HKCU comme un exe Win32 classique — non fait, non demandé pour l'instant.
        var commandLineArgs = Environment.GetCommandLineArgs().Skip(1).ToArray();
        var playRequest = CommandLineArgs.ParsePlay(commandLineArgs);
        if (playRequest is { Silent: true })
        {
            _ = RunSilentPlayAndExitAsync(playRequest);
            return;
        }

        var settings = _host.Services.GetRequiredService<ISettingsService>();

        var mainWindowViewModel = _host.Services.GetRequiredService<MainWindowViewModel>();
        _window = new MainWindow(mainWindowViewModel);
        ((AppWindowProvider)_host.Services.GetRequiredService<IAppWindowProvider>()).SetMainWindow(_window);

        ThemeManager.Initialize(_host.Services.GetRequiredService<IAppWindowProvider>());
        var effectiveTheme = settings.Current.Theme == AppSettings.SystemTheme
            ? (_host.Services.GetRequiredService<ISystemThemeProvider>().IsDarkThemeActive() ? AppSettings.DarkTheme : AppSettings.LightTheme)
            : settings.Current.Theme;
        ThemeManager.ApplyTheme(effectiveTheme);

        _host.Services.GetRequiredService<TrayIconService>();
        _host.Services.GetRequiredService<MacroHotkeyManager>().Initialize();

        var startHidden = commandLineArgs.Contains("--minimized", StringComparer.OrdinalIgnoreCase);
        if (!startHidden)
        {
            _window.Activate();
        }

        if (playRequest is not null)
        {
            // --play sans --silent : ouvre la macro dans l'éditeur et démarre la lecture, visible.
            var document = _host.Services.GetRequiredService<MacroDocumentViewModel>();
            await document.OpenFileAsync(playRequest.MacroFilePath);
            document.TogglePlaybackCommand.Execute(null);
        }
        else
        {
            var macroPath = commandLineArgs.FirstOrDefault(arg =>
                arg.EndsWith(Macro.FileExtension, StringComparison.OrdinalIgnoreCase) && File.Exists(arg));
            if (macroPath is not null)
            {
                await _host.Services.GetRequiredService<MacroDocumentViewModel>().OpenFileAsync(macroPath);
            }

            // TODO Phase 3 : guide de démarrage au premier lancement (HelpWindow pas encore porté).
        }
    }

    private async Task RunSilentPlayAndExitAsync(PlayRequest request)
    {
        try
        {
            var runner = _host!.Services.GetRequiredService<BackgroundMacroRunner>();
            await runner.RunAsync(request.MacroFilePath, request.RepeatOverride);
        }
        finally
        {
            Exit();
        }
    }

    private static void RegisterGlobalExceptionHandlers()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Log.Fatal(e.ExceptionObject as Exception, "Exception fatale non gérée.");

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Log.Error(e.Exception, "Exception de tâche non observée.");
            e.SetObserved();
        };
    }
}

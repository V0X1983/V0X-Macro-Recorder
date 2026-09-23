using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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

public partial class App : Application
{
    private IHost? _host;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

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

                services.AddSingleton<IDialogService, DialogService>();
                services.AddSingleton<ISecureInputPrompter, SecureInputPrompter>();
                services.AddSingleton<MacroDocumentViewModel>();
                services.AddSingleton<RecordingOptionsViewModel>();
                services.AddSingleton<PlaybackOptionsViewModel>();
                services.AddSingleton<MainWindowViewModel>();
                services.AddSingleton<MainWindow>();
                services.AddSingleton<BackgroundMacroRunner>();
                services.AddSingleton<MacroHotkeyManager>();
                services.AddSingleton<TrayIconService>();
            })
            .Build();

        RegisterGlobalExceptionHandlers();

        _host.Start();

        var settings = _host.Services.GetRequiredService<ISettingsService>();
        var effectiveTheme = settings.Current.Theme == AppSettings.SystemTheme
            ? (_host.Services.GetRequiredService<ISystemThemeProvider>().IsDarkThemeActive() ? AppSettings.DarkTheme : AppSettings.LightTheme)
            : settings.Current.Theme;
        ThemeManager.ApplyTheme(effectiveTheme);

        // Ligne de commande --play "chemin.v0xmacro" [--silent] [--repeat N] (étape 6, typiquement une tâche planifiée).
        var playRequest = CommandLineArgs.ParsePlay(e.Args);
        if (playRequest is { Silent: true })
        {
            // Jamais de fenêtre principale pour un run silencieux : ShutdownMode.OnExplicitShutdown évite que
            // l'absence de fenêtre ne ferme l'application avant la fin de la lecture (voir Shutdown() ci-dessous).
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            _ = RunSilentPlayAndExitAsync(playRequest);
            return;
        }

        // Nécessaire pour la réduction dans la zone de notification (fermer/masquer la fenêtre ne doit pas quitter
        // l'application) : voir TrayIconService et MainWindow.ExitForReal/OnClosing.
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        _host.Services.GetRequiredService<TrayIconService>();
        _host.Services.GetRequiredService<MacroHotkeyManager>().Initialize();

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        var startHidden = e.Args.Contains("--minimized", StringComparer.OrdinalIgnoreCase);
        if (!startHidden)
        {
            mainWindow.Show();
        }

        if (playRequest is not null)
        {
            // --play sans --silent : ouvre la macro dans l'éditeur et démarre la lecture, visible.
            var document = _host.Services.GetRequiredService<MacroDocumentViewModel>();
            document.OpenFile(playRequest.MacroFilePath);
            document.TogglePlaybackCommand.Execute(null);
        }
        else
        {
            // Ouverture d'une macro passée en argument (double-clic sur un fichier .v0xmacro).
            var macroPath = e.Args.FirstOrDefault(arg =>
                arg.EndsWith(Macro.FileExtension, StringComparison.OrdinalIgnoreCase) && File.Exists(arg));
            if (macroPath is not null)
            {
                _host.Services.GetRequiredService<MacroDocumentViewModel>().OpenFile(macroPath);
            }
            else if (!startHidden && !settings.Current.HasSeenWelcome)
            {
                // Premier lancement (jamais pour --minimized/--play, pour ne jamais interrompre un déclenchement automatisé) :
                // propose le guide de démarrage une seule fois.
                settings.Current.HasSeenWelcome = true;
                _ = settings.SaveAsync();
                var help = new Views.HelpWindow(_host.Services.GetRequiredService<IDialogService>()) { Owner = mainWindow };
                help.Show();
            }
        }
    }

    /// <summary>
    /// Run headless d'une tâche planifiée : jamais de MainWindow créée (jamais d'affichage), quitte toujours à la
    /// fin (succès, échec ou macro introuvable) pour ne jamais laisser un processus fantôme après un déclenchement
    /// planifié.
    /// </summary>
    private async Task RunSilentPlayAndExitAsync(PlayRequest request)
    {
        try
        {
            var runner = _host!.Services.GetRequiredService<BackgroundMacroRunner>();
            await runner.RunAsync(request.MacroFilePath, request.RepeatOverride);
        }
        finally
        {
            Shutdown();
        }
    }

    private void RegisterGlobalExceptionHandlers()
    {
        DispatcherUnhandledException += (_, args) =>
        {
            Log.Error(args.Exception, "Exception non gérée (UI).");
            MessageBox.Show(
                $"Une erreur inattendue s'est produite :\n{args.Exception.Message}\n\nDétails dans le journal.",
                "V0X Macro Recorder", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            Log.Fatal(args.ExceptionObject as Exception, "Exception fatale non gérée.");

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Log.Error(args.Exception, "Exception de tâche non observée.");
            args.SetObserved();
        };
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host?.Dispose();
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}

using Microsoft.Extensions.DependencyInjection;
using V0XMacroRecorder.Core.Abstractions;
using V0XMacroRecorder.Core.Macros;

namespace V0XMacroRecorder.Services;

/// <summary>
/// Point d'enregistrement unique des services (accès système, hooks, planificateur, mises à jour).
/// Chaque étape de la roadmap ajoute ses enregistrements ici plutôt que dans App.xaml.cs.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddV0XMacroRecorderServices(this IServiceCollection services)
    {
        services.AddSingleton<ISettingsService, SettingsService>();

        services.AddSingleton<InputHookThread>();
        services.AddSingleton<IGlobalHotKeyService>(sp => sp.GetRequiredService<InputHookThread>());
        services.AddSingleton<IActiveWindowProvider, Win32ActiveWindowProvider>();
        services.AddSingleton<ISecureInputGuard, Win32SecureInputGuard>();
        services.AddSingleton<IInputRecorder, Win32InputRecorder>();

        services.AddSingleton<IInputSimulator, Win32InputSimulator>();
        services.AddSingleton<IWindowFinder, Win32WindowFinder>();
        services.AddSingleton<IElevationService, Win32ElevationService>();
        services.AddSingleton<IKeyWaiter, Win32KeyWaiter>();
        services.AddSingleton<IClipboardService, Win32ClipboardService>();
        services.AddSingleton<IProcessLauncher, Win32ProcessLauncher>();
        services.AddSingleton<IWindowController, Win32WindowController>();
        services.AddSingleton<IPixelReader, Win32PixelReader>();
        services.AddSingleton<ISoundPlayer, Win32SoundPlayer>();
        services.AddSingleton<IMessageBoxService, Win32MessageBoxService>();
        services.AddSingleton<IScreenCapture, Win32ScreenCapture>();
        services.AddSingleton<IImageCodec, Win32ImageCodec>();
        services.AddSingleton<IImageSearcher, Win32ImageSearcher>();
        services.AddSingleton<IFileLineSource, FileLineSource>();
        services.AddSingleton<IMacroLoader, FileMacroLoader>();
        services.AddSingleton<IScriptRunner, RoslynScriptRunner>();
        services.AddSingleton<IStartupRegistrationService, Win32StartupRegistrationService>();
        services.AddSingleton<IMacroScheduler, Win32MacroScheduler>();
        services.AddSingleton<ISystemThemeProvider, Win32SystemThemeProvider>();
        services.AddSingleton<ISessionLockService, Win32SessionLockService>();
        services.AddSingleton<IDisplayInfoProvider, Win32DisplayInfoProvider>();
        services.AddSingleton<IDataProtector, Win32DataProtector>();
        services.AddSingleton<IUpdateChecker, GitHubUpdateChecker>();

        return services;
    }
}

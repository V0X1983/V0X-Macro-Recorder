using System.Diagnostics;
using Microsoft.Extensions.Logging;
using V0XMacroRecorder.Core.Abstractions;
using V0XMacroRecorder.Core.Playback;

namespace V0XMacroRecorder.App.Infrastructure;

/// <summary>
/// Joue une macro en arrière-plan, hors du document ouvert dans l'éditeur (étape 6) : raccourci global par macro,
/// menu de la zone de notification, ligne de commande <c>--play</c>. Un seul run à la fois (documenté, pas une
/// limite technique : évite deux lectures simultanées qui s'enverraient des entrées concurrentes). Journalise le
/// résultat (succès/échec, durée) dans le même journal Serilog que le reste de l'application.
/// </summary>
public sealed class BackgroundMacroRunner(
    IInputSimulator simulator,
    IWindowFinder windowFinder,
    IElevationService elevation,
    IKeyWaiter keyWaiter,
    IClipboardService clipboard,
    IProcessLauncher launcher,
    IWindowController windowController,
    IPixelReader pixelReader,
    ISoundPlayer soundPlayer,
    IMessageBoxService messageBox,
    IImageSearcher imageSearcher,
    IFileLineSource fileLines,
    IMacroLoader macroLoader,
    IScriptRunner scriptRunner,
    ISessionLockService sessionLock,
    ISecureInputPrompter securePrompter,
    IDataProtector dataProtector,
    ISettingsService settings,
    ILogger<BackgroundMacroRunner> logger)
{
    private CancellationTokenSource? _cts;

    public bool IsRunning => _cts is not null;

    /// <summary>Annule le run en cours (arrêt d'urgence), sans effet s'il n'y en a pas.</summary>
    public void Stop() => _cts?.Cancel();

    /// <summary>Charge et joue <paramref name="macroFilePath"/> jusqu'à la fin ; vrai si la lecture s'est terminée sans être annulée.</summary>
    public async Task<bool> RunAsync(string macroFilePath, int? repeatOverride = null, CancellationToken externalCancellation = default)
    {
        if (IsRunning)
        {
            logger.LogWarning("Lecture en arrière-plan déjà en cours : « {Path} » ignorée.", macroFilePath);
            return false;
        }

        var macro = macroLoader.Load(macroFilePath);
        if (macro is null)
        {
            logger.LogWarning("Macro introuvable ou illisible : « {Path} ».", macroFilePath);
            return false;
        }

        var options = settings.Current.Playback;
        var headlessOptions = new PlaybackOptions
        {
            SpeedMultiplier = options.SpeedMultiplier,
            NoDelay = options.NoDelay,
            RepeatCount = repeatOverride ?? options.RepeatCount,
            InfiniteLoop = repeatOverride is null && options.InfiniteLoop,
            DelayBetweenRepeatsMs = options.DelayBetweenRepeatsMs,
            WindowNotFoundAction = options.WindowNotFoundAction,
            WaitForWindowTimeoutMs = options.WaitForWindowTimeoutMs,
            StepMode = false, // Un run en arrière-plan n'a personne pour cliquer "Reprendre".
        };

        _cts = CancellationTokenSource.CreateLinkedTokenSource(externalCancellation);
        var player = new MacroPlayer(simulator, windowFinder, elevation, keyWaiter, clipboard, launcher, windowController,
            pixelReader, soundPlayer, messageBox, imageSearcher, fileLines, macroLoader,
            scriptRunner: scriptRunner, sessionLock: sessionLock, securePrompter: securePrompter, dataProtector: dataProtector);
        player.Warning += (_, warning) => logger.LogWarning("Macro « {Name} » : {Warning}", macro.Name, warning);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            await player.RunAsync(macro.Commands, headlessOptions, cancellationToken: _cts.Token, macroFilePath: macroFilePath).ConfigureAwait(false);
            logger.LogInformation("Macro « {Name} » terminée en arrière-plan (succès) en {Ms} ms.", macro.Name, stopwatch.ElapsedMilliseconds);
            return true;
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Macro « {Name} » annulée en arrière-plan après {Ms} ms.", macro.Name, stopwatch.ElapsedMilliseconds);
            return false;
        }
        finally
        {
            _cts.Dispose();
            _cts = null;
        }
    }
}

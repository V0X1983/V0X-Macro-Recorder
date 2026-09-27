using Microsoft.UI.Xaml.Controls;
using V0XMacroRecorder.App.ViewModels.Editors;
using V0XMacroRecorder.Core.Abstractions;
using V0XMacroRecorder.Core.Macros;
using V0XMacroRecorder.Core.Playback;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace V0XMacroRecorder.App.Infrastructure;

/// <summary>
/// Dialogues utilisateur, derrière une interface pour garder les ViewModels indépendants de l'UI (WPF à l'origine,
/// WinUI 3 ici). Entièrement asynchrone : contrairement à WPF, WinUI 3 n'a pas de dialogue modal bloquant — les
/// pickers <c>Windows.Storage.Pickers</c> et <see cref="ContentDialog"/> sont async par nature (voir
/// <see cref="Infrastructure.IDialogService"/>).
///
/// TODO Phase 3 (restant) : <see cref="EditCommandAsync"/>, <see cref="PickPixelColorAsync"/> et
/// <see cref="CaptureImageRegionAsync"/> dépendent chacun d'une fenêtre pas encore portée (<c>CommandEditorWindow</c>,
/// <c>ColorPickerOverlayWindow</c>, <c>RegionCaptureOverlayWindow</c>).
/// </summary>
public sealed class DialogService(
    IAppWindowProvider appWindow,
    IPixelReader pixelReader,
    IScreenCapture screenCapture,
    IImageCodec imageCodec,
    IInputSimulator inputSimulator,
    IWindowFinder windowFinder,
    IWindowController windowController,
    IClipboardService clipboard,
    IScriptRunner scriptRunner,
    ISettingsService settings,
    IDataProtector dataProtector,
    IImageSearcher imageSearcher) : IDialogService
{
    private const string AppTitle = "V0X Macro Recorder";
    private const int ImageTestPollIntervalMs = 250;

    private nint MainWindowHandle => WindowNative.GetWindowHandle(appWindow.MainWindow);

    public async Task<string?> PickOpenFileAsync()
    {
        var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.DocumentsLibrary };
        picker.FileTypeFilter.Add(Macro.FileExtension);
        InitializeWithWindow.Initialize(picker, MainWindowHandle);

        var file = await picker.PickSingleFileAsync();
        return file?.Path;
    }

    public async Task<string?> PickSaveFileAsync(string suggestedName)
    {
        var picker = new FileSavePicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            SuggestedFileName = suggestedName,
            DefaultFileExtension = Macro.FileExtension,
        };
        picker.FileTypeChoices.Add("Macros V0X", [Macro.FileExtension]);
        InitializeWithWindow.Initialize(picker, MainWindowHandle);

        var file = await picker.PickSaveFileAsync();
        return file?.Path;
    }

    public async Task<string?> PickFolderAsync(string title)
    {
        var picker = new FolderPicker { SuggestedStartLocation = PickerLocationId.DocumentsLibrary };
        picker.FileTypeFilter.Add("*");
        InitializeWithWindow.Initialize(picker, MainWindowHandle);

        var folder = await picker.PickSingleFolderAsync();
        return folder?.Path;
    }

    public async Task<string?> PickAnyFileAsync(string title, string filter)
    {
        var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.DocumentsLibrary };
        foreach (var extension in ParseExtensions(filter))
        {
            picker.FileTypeFilter.Add(extension);
        }

        InitializeWithWindow.Initialize(picker, MainWindowHandle);
        var file = await picker.PickSingleFileAsync();
        return file?.Path;
    }

    /// <summary>Convertit un filtre WPF ("Nom (*.ext1;*.ext2)|*.ext1;*.ext2|...") en extensions pour <see cref="FileOpenPicker.FileTypeFilter"/> ("*" = tous fichiers).</summary>
    private static IEnumerable<string> ParseExtensions(string wpfFilter)
    {
        var parts = wpfFilter.Split('|');
        var extensions = new List<string>();
        for (var i = 1; i < parts.Length; i += 2)
        {
            foreach (var pattern in parts[i].Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = pattern.Trim();
                extensions.Add(trimmed is "*.*" or "*" ? "*" : trimmed.TrimStart('*'));
            }
        }

        return extensions.Count > 0 ? extensions.Distinct() : ["*"];
    }

    public async Task<UnsavedChangesChoice> AskSaveChangesAsync(string macroName)
    {
        var dialog = new ContentDialog
        {
            Title = AppTitle,
            Content = $"Enregistrer les modifications de « {macroName} » avant de continuer ?",
            PrimaryButtonText = "Enregistrer",
            SecondaryButtonText = "Ne pas enregistrer",
            CloseButtonText = "Annuler",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = appWindow.MainWindow.Content.XamlRoot,
        };

        return await dialog.ShowAsync() switch
        {
            ContentDialogResult.Primary => UnsavedChangesChoice.Save,
            ContentDialogResult.Secondary => UnsavedChangesChoice.Discard,
            _ => UnsavedChangesChoice.Cancel,
        };
    }

    public async Task ShowErrorAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = "OK",
            XamlRoot = appWindow.MainWindow.Content.XamlRoot,
        };

        await dialog.ShowAsync();
    }

    public async Task<MacroCommand?> EditCommandAsync(string kind, MacroCommand? existing, IReadOnlyList<string>? knownLabels = null)
    {
        var editor = ViewModels.Editors.CommandEditorViewModel.Create(kind, existing, this, knownLabels, dataProtector);
        var window = new Views.CommandEditorWindow(editor);
        return await window.ShowAndWaitAsync() ? editor.Build() : null;
    }

    public Task<PixelColorPickResult?> PickPixelColorAsync() =>
        new Views.ColorPickerOverlayWindow(pixelReader, screenCapture).ShowAndWaitAsync();

    public Task<ImageCaptureResult?> CaptureImageRegionAsync() =>
        new Views.RegionCaptureOverlayWindow(screenCapture, imageCodec).ShowAndWaitAsync();

    /// <summary>Variables jetables : le bouton « Tester » n'a pas de contexte de lecture en cours (pas de macro qui joue). Logique métier inchangée, ne dépend d'aucune Window.</summary>
    public async Task<ScriptTestResult> TestScriptAsync(string code, int timeoutSeconds)
    {
        var globals = new ScriptGlobals(inputSimulator, windowFinder, windowController, clipboard, new VariableStore(), _ => { }, CancellationToken.None);
        var result = await scriptRunner.RunAsync(code, globals, Math.Max(1, timeoutSeconds) * 1000, CancellationToken.None).ConfigureAwait(false);
        return new ScriptTestResult(result.Success, result.ErrorMessage);
    }

    /// <summary>
    /// Recherche plein écran (comme à la lecture réelle) ; ne clique jamais. Logique métier inchangée, ne dépend
    /// d'aucune Window. Tout entier dans <see cref="Task.Run(Func{Task})"/> (contrairement à un simple `await
    /// Task.Delay(...).ConfigureAwait(false)` entre chaque essai) : sans ça, le tout premier <c>imageSearcher.Find</c>
    /// (recherche plein écran, potentiellement multi-écran) s'exécute de façon synchrone sur le thread UI — appelant
    /// (le bouton « Tester » l'attend directement, sans <c>Task.Run</c> de son côté), gelant l'app le temps de cette
    /// première recherche. Bug réel signalé par l'utilisateur (« ça freeze »).
    /// </summary>
    public Task<ImageSearchTestResult> TestImageSearchAsync(string templatePngBase64, int tolerancePercent, int timeoutMs) => Task.Run(() =>
    {
        var template = Convert.FromBase64String(templatePngBase64);
        var elapsed = 0;
        while (true)
        {
            if (imageSearcher.Find(template, null, tolerancePercent) is { } position)
            {
                return new ImageSearchTestResult(true, position.X, position.Y);
            }

            if (elapsed >= timeoutMs)
            {
                return new ImageSearchTestResult(false, null, null);
            }

            Thread.Sleep(ImageTestPollIntervalMs);
            elapsed += ImageTestPollIntervalMs;
        }
    });

    public async Task<bool> ConfirmOpenUntrustedMacroAsync(string macroName)
    {
        var dialog = new ContentDialog
        {
            Title = AppTitle,
            Content = $"« {macroName} » contient un script C# et/ou une commande « Lancer un programme/commande shell » : elle peut exécuter n'importe quel code sur cet ordinateur.\n\nN'ouvrez cette macro que si vous faites confiance à sa source. Continuer ?",
            PrimaryButtonText = "Non",
            SecondaryButtonText = "Oui",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = appWindow.MainWindow.Content.XamlRoot,
        };

        return await dialog.ShowAsync() == ContentDialogResult.Secondary;
    }
}

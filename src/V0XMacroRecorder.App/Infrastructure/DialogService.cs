using System.IO;
using System.Windows;
using Microsoft.Win32;
using V0XMacroRecorder.App.ViewModels.Editors;
using V0XMacroRecorder.App.Views;
using V0XMacroRecorder.Core.Abstractions;
using V0XMacroRecorder.Core.Macros;
using V0XMacroRecorder.Core.Playback;

namespace V0XMacroRecorder.App.Infrastructure;

public sealed class DialogService(
    IPixelReader pixelReader,
    IScreenCapture screenCapture,
    IImageCodec imageCodec,
    IInputSimulator inputSimulator,
    IWindowFinder windowFinder,
    IWindowController windowController,
    IClipboardService clipboard,
    IScriptRunner scriptRunner,
    ISettingsService settings,
    IDataProtector dataProtector) : IDialogService
{
    private const string AppTitle = "V0X Macro Recorder";
    private const string FileFilter = "Macros V0X (*.v0xmacro)|*.v0xmacro|Tous les fichiers (*.*)|*.*";

    private static Window? Owner => Application.Current.MainWindow;

    /// <summary>Dossier proposé par défaut (Paramètres > Dossier des macros) quand Windows n'a pas déjà un dossier « dernier utilisé » pour ce type de boîte.</summary>
    private string? InitialMacroDirectory =>
        Directory.Exists(settings.Current.DefaultMacrosFolder) ? settings.Current.DefaultMacrosFolder : null;

    public string? PickOpenFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Ouvrir une macro",
            Filter = FileFilter,
            DefaultExt = Macro.FileExtension,
            CheckFileExists = true,
            InitialDirectory = InitialMacroDirectory,
        };

        return dialog.ShowDialog(Owner) == true ? dialog.FileName : null;
    }

    public string? PickSaveFile(string suggestedName)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Enregistrer la macro",
            Filter = FileFilter,
            DefaultExt = Macro.FileExtension,
            AddExtension = true,
            OverwritePrompt = true,
            FileName = suggestedName,
            InitialDirectory = InitialMacroDirectory,
        };

        return dialog.ShowDialog(Owner) == true ? dialog.FileName : null;
    }

    public string? PickFolder(string title)
    {
        var dialog = new OpenFolderDialog { Title = title, InitialDirectory = InitialMacroDirectory };
        return dialog.ShowDialog(Owner) == true ? dialog.FolderName : null;
    }

    public string? PickAnyFile(string title, string filter)
    {
        var dialog = new OpenFileDialog
        {
            Title = title,
            Filter = filter,
            CheckFileExists = true,
        };

        return dialog.ShowDialog(Owner) == true ? dialog.FileName : null;
    }

    public UnsavedChangesChoice AskSaveChanges(string macroName)
    {
        var result = MessageBox.Show(
            Owner!,
            $"Enregistrer les modifications de « {macroName} » avant de continuer ?",
            AppTitle,
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Question);

        return result switch
        {
            System.Windows.MessageBoxResult.Yes => UnsavedChangesChoice.Save,
            System.Windows.MessageBoxResult.No => UnsavedChangesChoice.Discard,
            _ => UnsavedChangesChoice.Cancel,
        };
    }

    public void ShowError(string title, string message) =>
        MessageBox.Show(Owner!, message, title, MessageBoxButton.OK, MessageBoxImage.Warning);

    public MacroCommand? EditCommand(string kind, MacroCommand? existing, IReadOnlyList<string>? knownLabels = null)
    {
        var editor = CommandEditorViewModel.Create(kind, existing, this, knownLabels, dataProtector);
        var window = new CommandEditorWindow(editor) { Owner = Owner };
        return window.ShowDialog() == true ? editor.Build() : null;
    }

    public PixelColorPickResult? PickPixelColor()
    {
        var window = new ColorPickerOverlayWindow(pixelReader);
        return window.ShowDialog() == true ? new PixelColorPickResult(window.PickedX, window.PickedY, window.PickedColorHex) : null;
    }

    public ImageCaptureResult? CaptureImageRegion()
    {
        var window = new RegionCaptureOverlayWindow(screenCapture, imageCodec);
        if (window.ShowDialog() != true || window.CapturedRegion is not { } region)
        {
            return null;
        }

        return new ImageCaptureResult(window.CapturedPngBase64, region.Width, region.Height, region);
    }

    /// <summary>Variables jetables : le bouton « Tester » n'a pas de contexte de lecture en cours (pas de macro qui joue).</summary>
    public async Task<ScriptTestResult> TestScriptAsync(string code, int timeoutSeconds)
    {
        var globals = new ScriptGlobals(inputSimulator, windowFinder, windowController, clipboard, new VariableStore(), _ => { }, CancellationToken.None);
        var result = await scriptRunner.RunAsync(code, globals, Math.Max(1, timeoutSeconds) * 1000, CancellationToken.None).ConfigureAwait(false);
        return new ScriptTestResult(result.Success, result.ErrorMessage);
    }

    public bool ConfirmOpenUntrustedMacro(string macroName)
    {
        var result = MessageBox.Show(
            Owner!,
            $"« {macroName} » contient un script C# et/ou une commande « Lancer un programme/commande shell » : elle peut exécuter n'importe quel code sur cet ordinateur.\n\nN'ouvrez cette macro que si vous faites confiance à sa source. Continuer ?",
            AppTitle,
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            System.Windows.MessageBoxResult.No);

        return result == System.Windows.MessageBoxResult.Yes;
    }
}

using System.Runtime.InteropServices;
using Windows.ApplicationModel.DataTransfer;
using V0XMacroRecorder.Core.Macros;

namespace V0XMacroRecorder.App.Infrastructure;

/// <summary>
/// Copier/coller de commandes entre macros et entre instances, dans un format propre à V0X Macro Recorder — via
/// <see cref="Clipboard"/>/<see cref="DataPackage"/> (Windows.ApplicationModel.DataTransfer), l'équivalent WinUI 3
/// du presse-papiers WPF à format personnalisé (<c>System.Windows.DataObject</c>).
/// </summary>
public static class MacroClipboard
{
    private const string Format = "V0XMacroRecorder.Commands";

    public static bool TrySetCommands(IEnumerable<MacroCommand> commands)
    {
        try
        {
            var data = new DataPackage();
            data.SetData(Format, MacroSerializer.SerializeCommands(commands));
            Clipboard.SetContent(data);
            return true;
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException)
        {
            // Presse-papiers verrouillé par une autre application.
            return false;
        }
    }

    /// <summary>
    /// Commandes présentes dans le presse-papiers, ou null s'il n'y en a pas (ou si le contenu est invalide).
    /// Asynchrone : contrairement à WPF (<c>Clipboard.GetData</c>, synchrone), <see cref="DataPackageView.GetDataAsync"/>
    /// ne l'est pas en WinUI 3 — le bloquer synchroniquement risquerait un blocage (réentrance COM/STA).
    /// </summary>
    public static async Task<IReadOnlyList<MacroCommand>?> TryGetCommandsAsync()
    {
        try
        {
            var view = Clipboard.GetContent();
            if (!view.Contains(Format))
            {
                return null;
            }

            if (await view.GetDataAsync(Format) is not string json)
            {
                return null;
            }

            var commands = MacroSerializer.DeserializeCommands(json);
            return commands.Count > 0 ? commands : null;
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException or MacroFormatException)
        {
            return null;
        }
    }
}

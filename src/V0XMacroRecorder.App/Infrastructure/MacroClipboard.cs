using System.Runtime.InteropServices;
using System.Windows;
using V0XMacroRecorder.Core.Macros;

namespace V0XMacroRecorder.App.Infrastructure;

/// <summary>Copier/coller de commandes entre macros et entre instances, dans un format propre à V0X Macro Recorder.</summary>
public static class MacroClipboard
{
    private const string Format = "V0XMacroRecorder.Commands";

    public static bool TrySetCommands(IEnumerable<MacroCommand> commands)
    {
        try
        {
            var data = new DataObject();
            data.SetData(Format, MacroSerializer.SerializeCommands(commands));
            Clipboard.SetDataObject(data, copy: true);
            return true;
        }
        catch (Exception ex) when (ex is ExternalException or InvalidOperationException)
        {
            // Presse-papiers verrouillé par une autre application.
            return false;
        }
    }

    /// <summary>Commandes présentes dans le presse-papiers, ou null s'il n'y en a pas (ou si le contenu est invalide).</summary>
    public static IReadOnlyList<MacroCommand>? TryGetCommands()
    {
        try
        {
            if (!Clipboard.ContainsData(Format) || Clipboard.GetData(Format) is not string json)
            {
                return null;
            }

            var commands = MacroSerializer.DeserializeCommands(json);
            return commands.Count > 0 ? commands : null;
        }
        catch (Exception ex) when (ex is ExternalException or InvalidOperationException or MacroFormatException)
        {
            return null;
        }
    }
}

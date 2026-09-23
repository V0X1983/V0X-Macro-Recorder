using System.Runtime.InteropServices;
using System.Windows;
using V0XMacroRecorder.Core.Abstractions;

namespace V0XMacroRecorder.Services;

/// <summary>
/// Presse-papiers via WPF (<see cref="Clipboard"/>). Le presse-papiers Windows peut être verrouillé
/// momentanément par une autre application : un essai supplémentaire absorbe ce cas fréquent plutôt que
/// de faire échouer toute la lecture pour un incident transitoire.
/// </summary>
public sealed class Win32ClipboardService : IClipboardService
{
    public string GetText()
    {
        try
        {
            return Clipboard.GetText();
        }
        catch (COMException)
        {
            try
            {
                return Clipboard.GetText();
            }
            catch (COMException)
            {
                return "";
            }
        }
    }

    public void SetText(string text)
    {
        try
        {
            Clipboard.SetText(text ?? "");
        }
        catch (COMException)
        {
            try
            {
                Clipboard.SetText(text ?? "");
            }
            catch (COMException)
            {
                // Presse-papiers indisponible malgré la nouvelle tentative : abandon silencieux, cf. Warning côté MacroPlayer.
            }
        }
    }
}

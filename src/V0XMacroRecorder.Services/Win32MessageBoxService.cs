using CoreAbstractions = V0XMacroRecorder.Core.Abstractions;
using V0XMacroRecorder.Core.Macros;
using V0XMacroRecorder.Services.Native;

namespace V0XMacroRecorder.Services;

/// <summary>
/// Boîte de message Win32 native (<c>MessageBoxW</c>) : indépendante de toute UI applicative (WPF/WinUI), et
/// utilisable directement depuis un thread d'arrière-plan (MacroPlayer) sans marshaling vers un thread UI —
/// contrairement à une boîte de dialogue de framework, MessageBoxW gère sa propre boucle de messages.
/// </summary>
public sealed class Win32MessageBoxService : CoreAbstractions.IMessageBoxService
{
    public CoreAbstractions.MessageBoxResult Show(string title, string text, MessageBoxKind kind)
    {
        var icon = kind switch
        {
            MessageBoxKind.Warning => NativeMethods.MB_ICONWARNING,
            MessageBoxKind.Error => NativeMethods.MB_ICONERROR,
            _ => NativeMethods.MB_ICONINFORMATION,
        };
        var button = kind == MessageBoxKind.OkCancel ? NativeMethods.MB_OKCANCEL : NativeMethods.MB_OK;
        var flags = icon | button | NativeMethods.MB_SETFOREGROUND | NativeMethods.MB_TOPMOST;

        var result = NativeMethods.MessageBoxW(IntPtr.Zero, text, title, flags);
        return result == NativeMethods.IDCANCEL
            ? CoreAbstractions.MessageBoxResult.Cancel
            : CoreAbstractions.MessageBoxResult.Ok;
    }
}

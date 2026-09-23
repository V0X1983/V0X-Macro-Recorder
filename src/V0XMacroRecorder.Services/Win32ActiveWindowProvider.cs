using System.Text;
using V0XMacroRecorder.Core.Abstractions;
using V0XMacroRecorder.Core.Recording;
using V0XMacroRecorder.Services.Native;

namespace V0XMacroRecorder.Services;

/// <summary>Résout la fenêtre active via Win32 (utilisée pour le repère de coordonnées « Fenêtre active »).</summary>
public sealed class Win32ActiveWindowProvider : IActiveWindowProvider
{
    public ActiveWindowInfo? GetActiveWindow()
    {
        var hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == IntPtr.Zero || !NativeMethods.GetWindowRect(hwnd, out var rect))
        {
            return null;
        }

        var classBuffer = new StringBuilder(256);
        NativeMethods.GetClassNameW(hwnd, classBuffer, classBuffer.Capacity);

        var titleLength = Math.Max(0, NativeMethods.GetWindowTextLengthW(hwnd));
        var titleBuffer = new StringBuilder(titleLength + 1);
        NativeMethods.GetWindowTextW(hwnd, titleBuffer, titleBuffer.Capacity);

        return new ActiveWindowInfo(titleBuffer.ToString(), classBuffer.ToString(), rect.Left, rect.Top);
    }
}

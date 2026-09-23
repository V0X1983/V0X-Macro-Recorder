using V0XMacroRecorder.Core.Abstractions;
using V0XMacroRecorder.Services.Native;

namespace V0XMacroRecorder.Services;

/// <summary>Réutilise les mêmes constantes <c>GetSystemMetrics</c> que <see cref="Win32InputSimulator"/> pour la normalisation des coordonnées.</summary>
public sealed class Win32DisplayInfoProvider : IDisplayInfoProvider
{
    public (int Width, int Height) GetVirtualScreenSize()
    {
        var width = Math.Max(1, NativeMethods.GetSystemMetrics(NativeMethods.SM_CXVIRTUALSCREEN));
        var height = Math.Max(1, NativeMethods.GetSystemMetrics(NativeMethods.SM_CYVIRTUALSCREEN));
        return (width, height);
    }
}

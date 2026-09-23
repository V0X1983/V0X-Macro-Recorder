using V0XMacroRecorder.Core.Abstractions;
using V0XMacroRecorder.Services.Native;

namespace V0XMacroRecorder.Services;

/// <summary>
/// <c>OpenInputDesktop</c> échoue précisément quand le thread appelant ne peut pas accéder au bureau interactif
/// (écran de verrouillage, UAC Secure Desktop, changement rapide d'utilisateur) : technique Win32 standard,
/// aucune autre API ne donne cette information de façon plus directe.
/// </summary>
public sealed class Win32SessionLockService : ISessionLockService
{
    public bool IsInputSessionLocked()
    {
        var desktop = NativeMethods.OpenInputDesktop(0, false, NativeMethods.DESKTOP_SWITCHDESKTOP);
        if (desktop == IntPtr.Zero)
        {
            return true;
        }

        NativeMethods.CloseDesktop(desktop);
        return false;
    }
}

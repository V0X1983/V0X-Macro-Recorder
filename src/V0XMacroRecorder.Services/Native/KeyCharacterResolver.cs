using System.Text;

namespace V0XMacroRecorder.Services.Native;

/// <summary>
/// Résout le caractère produit par une touche selon la disposition clavier active (AZERTY, QWERTY…) et les
/// modificateurs actuellement enfoncés (Maj, Verr. maj), via ToUnicodeEx. Appelé hors du callback du hook
/// (sur le thread consommateur de <see cref="InputHookThread"/>), jamais depuis la procédure de hook elle-même.
/// </summary>
internal sealed class KeyCharacterResolver
{
    /// <summary>Renvoie le caractère produit par cette touche, ou null si elle n'en produit pas (touche morte, touche de fonction…).</summary>
    public char? TryResolve(int virtualKey, int scanCode, bool isExtended)
    {
        var foreground = NativeMethods.GetForegroundWindow();
        var threadId = foreground == IntPtr.Zero ? 0u : NativeMethods.GetWindowThreadProcessId(foreground, out _);
        var layout = NativeMethods.GetKeyboardLayout(threadId);

        var keyState = new byte[256];
        SetIfDown(keyState, NativeMethods.VK_SHIFT);
        SetIfDown(keyState, NativeMethods.VK_LSHIFT);
        SetIfDown(keyState, NativeMethods.VK_RSHIFT);
        SetIfDown(keyState, NativeMethods.VK_CONTROL);
        SetIfDown(keyState, NativeMethods.VK_LCONTROL);
        SetIfDown(keyState, NativeMethods.VK_RCONTROL);
        SetIfDown(keyState, NativeMethods.VK_MENU);
        SetIfDown(keyState, NativeMethods.VK_LMENU);
        SetIfDown(keyState, NativeMethods.VK_RMENU);
        if ((NativeMethods.GetKeyState((int)NativeMethods.VK_CAPITAL) & 1) != 0)
        {
            keyState[NativeMethods.VK_CAPITAL] = 1;
        }

        var scanCodeEx = (uint)scanCode | (isExtended ? 0xE000u : 0u);
        var buffer = new StringBuilder(8);
        var result = NativeMethods.ToUnicodeEx((uint)virtualKey, scanCodeEx, keyState, buffer, buffer.Capacity, 0, layout);

        // result > 0 : un caractère (ou plus, ligature rare - on ne garde que le premier).
        // result == 0 : aucune traduction (touches de fonction, flèches…). result < 0 : touche morte (accent seul) : ignorée.
        return result > 0 ? buffer[0] : null;
    }

    private static void SetIfDown(byte[] keyState, uint virtualKey)
    {
        if ((NativeMethods.GetAsyncKeyState((int)virtualKey) & 0x8000) != 0)
        {
            keyState[virtualKey] = 0x80;
        }
    }
}

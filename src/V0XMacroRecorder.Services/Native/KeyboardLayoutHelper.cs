namespace V0XMacroRecorder.Services.Native;

/// <summary>Résout le code de balayage d'une touche virtuelle selon la disposition active (repli utilisé quand une commande n'a pas de scan code connu, ex. créée manuellement).</summary>
internal static class KeyboardLayoutHelper
{
    /// <summary>Code de balayage (bas octet) et indicateur « touche étendue » (bit 0x100 du résultat de MapVirtualKeyEx).</summary>
    public static (int ScanCode, bool Extended) ResolveScanCode(int virtualKey)
    {
        var foreground = NativeMethods.GetForegroundWindow();
        var threadId = foreground == IntPtr.Zero ? 0u : NativeMethods.GetWindowThreadProcessId(foreground, out _);
        var layout = NativeMethods.GetKeyboardLayout(threadId);
        var result = NativeMethods.MapVirtualKeyExW((uint)virtualKey, NativeMethods.MAPVK_VK_TO_VSC_EX, layout);
        return ((int)(result & 0xFF), (result & 0x100) != 0);
    }
}

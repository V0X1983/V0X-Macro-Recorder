using V0XMacroRecorder.Core.Abstractions;
using V0XMacroRecorder.Services.Native;

namespace V0XMacroRecorder.Services;

/// <summary>
/// Presse-papiers via l'API Win32 directe (pas de dépendance à une UI particulière). Le presse-papiers Windows
/// peut être verrouillé momentanément par une autre application : un essai supplémentaire absorbe ce cas fréquent
/// plutôt que de faire échouer toute la lecture pour un incident transitoire.
/// </summary>
public sealed class Win32ClipboardService : IClipboardService
{
    public string GetText()
    {
        return TryGetText(out var text) || TryGetText(out text) ? text : "";
    }

    public void SetText(string text)
    {
        if (!TrySetText(text ?? "") )
        {
            TrySetText(text ?? "");
        }
    }

    private static bool TryGetText(out string text)
    {
        text = "";
        if (!NativeMethods.OpenClipboard(IntPtr.Zero))
        {
            return false;
        }

        try
        {
            var handle = NativeMethods.GetClipboardData(NativeMethods.CF_UNICODETEXT);
            if (handle == IntPtr.Zero)
            {
                return true; // Presse-papiers ouvert mais vide/sans texte : chaîne vide, pas un échec transitoire.
            }

            var pointer = NativeMethods.GlobalLock(handle);
            if (pointer == IntPtr.Zero)
            {
                return false;
            }

            try
            {
                text = System.Runtime.InteropServices.Marshal.PtrToStringUni(pointer) ?? "";
                return true;
            }
            finally
            {
                NativeMethods.GlobalUnlock(handle);
            }
        }
        finally
        {
            NativeMethods.CloseClipboard();
        }
    }

    private static bool TrySetText(string text)
    {
        if (!NativeMethods.OpenClipboard(IntPtr.Zero))
        {
            return false;
        }

        try
        {
            NativeMethods.EmptyClipboard();

            var byteCount = (text.Length + 1) * sizeof(char);
            var handle = NativeMethods.GlobalAlloc(NativeMethods.GMEM_MOVEABLE, (UIntPtr)byteCount);
            if (handle == IntPtr.Zero)
            {
                return false;
            }

            var pointer = NativeMethods.GlobalLock(handle);
            if (pointer == IntPtr.Zero)
            {
                return false;
            }

            System.Runtime.InteropServices.Marshal.Copy((text + '\0').ToCharArray(), 0, pointer, text.Length + 1);
            NativeMethods.GlobalUnlock(handle);

            // Le presse-papiers devient propriétaire de `handle` dès que SetClipboardData réussit : ne jamais le
            // libérer nous-mêmes ensuite (GlobalFree), sous peine de le corrompre pour les autres applications.
            return NativeMethods.SetClipboardData(NativeMethods.CF_UNICODETEXT, handle) != IntPtr.Zero;
        }
        finally
        {
            NativeMethods.CloseClipboard();
        }
    }
}

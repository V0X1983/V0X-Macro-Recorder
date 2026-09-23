using System.Text;
using V0XMacroRecorder.Core.Abstractions;
using V0XMacroRecorder.Services.Native;

namespace V0XMacroRecorder.Services;

/// <summary>Retrouve une fenêtre par titre/classe à la lecture (repère « Fenêtre active »), par énumération Win32 (ordre Z : la plus au premier plan d'abord).</summary>
public sealed class Win32WindowFinder : IWindowFinder
{
    public nint? FindWindow(string? titleContains, string? className)
    {
        if (string.IsNullOrEmpty(titleContains) && string.IsNullOrEmpty(className))
        {
            return null;
        }

        nint? match = null;
        NativeMethods.EnumWindows((hwnd, _) =>
        {
            if (!NativeMethods.IsWindowVisible(hwnd))
            {
                return true; // continuer l'énumération.
            }

            var style = NativeMethods.GetWindowLongPtrW(hwnd, NativeMethods.GWL_EXSTYLE);
            if ((style & NativeMethods.WS_EX_TOOLWINDOW) != 0)
            {
                return true; // fenêtre outil (ex. barre d'options d'une palette) : jamais une cible pertinente.
            }

            if (!string.IsNullOrEmpty(className) && !string.Equals(GetClassName(hwnd), className, StringComparison.Ordinal))
            {
                return true;
            }

            if (!string.IsNullOrEmpty(titleContains)
                && !GetWindowTitle(hwnd).Contains(titleContains, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            match = hwnd;
            return false; // trouvé : arrêter l'énumération.
        }, IntPtr.Zero);

        return match;
    }

    public (int Left, int Top)? GetTopLeft(nint window) =>
        NativeMethods.GetWindowRect(window, out var rect) ? (rect.Left, rect.Top) : null;

    public bool Activate(nint window)
    {
        if (NativeMethods.IsIconic(window))
        {
            NativeMethods.ShowWindow(window, NativeMethods.SW_RESTORE);
        }

        return NativeMethods.SetForegroundWindow(window);
    }

    public bool? IsProbablyElevated(nint window)
    {
        var threadId = NativeMethods.GetWindowThreadProcessId(window, out var processId);
        if (threadId == 0 || processId == 0)
        {
            return null;
        }

        var process = NativeMethods.OpenProcess(NativeMethods.PROCESS_QUERY_LIMITED_INFORMATION, false, processId);
        if (process == IntPtr.Zero)
        {
            // Accès refusé à un processus qu'on ne peut même pas interroger : signe probable d'un privilège supérieur.
            return true;
        }

        try
        {
            if (!NativeMethods.OpenProcessToken(process, NativeMethods.TOKEN_QUERY, out var token))
            {
                return null;
            }

            try
            {
                return NativeMethods.GetTokenInformation(token, NativeMethods.TokenElevation, out var elevation, sizeof(int), out _)
                    ? elevation != 0
                    : null;
            }
            finally
            {
                NativeMethods.CloseHandle(token);
            }
        }
        finally
        {
            NativeMethods.CloseHandle(process);
        }
    }

    private static string GetClassName(nint hwnd)
    {
        var buffer = new StringBuilder(256);
        NativeMethods.GetClassNameW(hwnd, buffer, buffer.Capacity);
        return buffer.ToString();
    }

    private static string GetWindowTitle(nint hwnd)
    {
        var length = Math.Max(0, NativeMethods.GetWindowTextLengthW(hwnd));
        var buffer = new StringBuilder(length + 1);
        NativeMethods.GetWindowTextW(hwnd, buffer, buffer.Capacity);
        return buffer.ToString();
    }
}

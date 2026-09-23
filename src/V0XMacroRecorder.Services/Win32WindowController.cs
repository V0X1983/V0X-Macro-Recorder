using V0XMacroRecorder.Core.Abstractions;
using V0XMacroRecorder.Services.Native;

namespace V0XMacroRecorder.Services;

public sealed class Win32WindowController : IWindowController
{
    public bool Minimize(nint window) => NativeMethods.ShowWindow(window, NativeMethods.SW_MINIMIZE);

    public bool Maximize(nint window) => NativeMethods.ShowWindow(window, NativeMethods.SW_MAXIMIZE);

    public bool Restore(nint window) => NativeMethods.ShowWindow(window, NativeMethods.SW_RESTORE);

    public bool Close(nint window) => NativeMethods.PostMessageW(window, NativeMethods.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);

    public bool MoveResize(nint window, int x, int y, int width, int height) =>
        NativeMethods.SetWindowPos(window, IntPtr.Zero, x, y, width, height, NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);
}

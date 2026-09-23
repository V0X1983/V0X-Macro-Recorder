using V0XMacroRecorder.Core.Abstractions;
using V0XMacroRecorder.Services.Native;
using CoreMouseButton = V0XMacroRecorder.Core.Macros.MouseButton;

namespace V0XMacroRecorder.Services;

/// <summary>Injection Win32 réelle (SendInput) : souris en coordonnées absolues normalisées (multi-écran), clavier par code de balayage, texte en Unicode direct.</summary>
public sealed class Win32InputSimulator : IInputSimulator
{
    public void MoveMouseTo(int screenX, int screenY)
    {
        var (nx, ny) = NormalizeToVirtualDesktop(screenX, screenY);
        Send(MouseInput(nx, ny, 0, NativeMethods.MOUSEEVENTF_MOVE | NativeMethods.MOUSEEVENTF_ABSOLUTE | NativeMethods.MOUSEEVENTF_VIRTUALDESK));
    }

    public (int X, int Y) GetCursorPosition()
    {
        NativeMethods.GetCursorPos(out var point);
        return (point.X, point.Y);
    }

    public void MouseButton(CoreMouseButton button, bool down)
    {
        var flag = (button, down) switch
        {
            (CoreMouseButton.Left, true) => NativeMethods.MOUSEEVENTF_LEFTDOWN,
            (CoreMouseButton.Left, false) => NativeMethods.MOUSEEVENTF_LEFTUP,
            (CoreMouseButton.Right, true) => NativeMethods.MOUSEEVENTF_RIGHTDOWN,
            (CoreMouseButton.Right, false) => NativeMethods.MOUSEEVENTF_RIGHTUP,
            (CoreMouseButton.Middle, true) => NativeMethods.MOUSEEVENTF_MIDDLEDOWN,
            (CoreMouseButton.Middle, false) => NativeMethods.MOUSEEVENTF_MIDDLEUP,
            _ => 0u,
        };

        Send(MouseInput(0, 0, 0, flag));
    }

    public void MouseWheel(int notches) =>
        Send(MouseInput(0, 0, unchecked((uint)(notches * (int)NativeMethods.WHEEL_DELTA)), NativeMethods.MOUSEEVENTF_WHEEL));

    public void KeyEvent(int virtualKey, int scanCode, bool isExtended, bool down)
    {
        if (scanCode == 0 && virtualKey != 0)
        {
            (scanCode, isExtended) = KeyboardLayoutHelper.ResolveScanCode(virtualKey);
        }

        var flags = NativeMethods.KEYEVENTF_SCANCODE | (isExtended ? NativeMethods.KEYEVENTF_EXTENDEDKEY : 0) | (down ? 0 : NativeMethods.KEYEVENTF_KEYUP);
        Send(KeyboardInput(0, (ushort)scanCode, flags));
    }

    public void TypeCharacter(char character)
    {
        Send(KeyboardInput(0, character, NativeMethods.KEYEVENTF_UNICODE));
        Send(KeyboardInput(0, character, NativeMethods.KEYEVENTF_UNICODE | NativeMethods.KEYEVENTF_KEYUP));
    }

    private static (int X, int Y) NormalizeToVirtualDesktop(int screenX, int screenY)
    {
        var left = NativeMethods.GetSystemMetrics(NativeMethods.SM_XVIRTUALSCREEN);
        var top = NativeMethods.GetSystemMetrics(NativeMethods.SM_YVIRTUALSCREEN);
        var width = Math.Max(1, NativeMethods.GetSystemMetrics(NativeMethods.SM_CXVIRTUALSCREEN));
        var height = Math.Max(1, NativeMethods.GetSystemMetrics(NativeMethods.SM_CYVIRTUALSCREEN));

        // +1 : la valeur normalisée doit correspondre au pixel visé même à l'extrémité de l'écran (arrondi classique SendInput).
        var nx = (int)Math.Round((screenX - left) * 65536.0 / width) + 1;
        var ny = (int)Math.Round((screenY - top) * 65536.0 / height) + 1;
        return (Math.Clamp(nx, 0, 65535), Math.Clamp(ny, 0, 65535));
    }

    private static NativeMethods.INPUT MouseInput(int dx, int dy, uint mouseData, uint flags) => new()
    {
        type = NativeMethods.INPUT_MOUSE,
        U = new NativeMethods.InputUnion
        {
            mi = new NativeMethods.MOUSEINPUT { dx = dx, dy = dy, mouseData = mouseData, dwFlags = flags },
        },
    };

    private static NativeMethods.INPUT KeyboardInput(ushort virtualKey, ushort scanCode, uint flags) => new()
    {
        type = NativeMethods.INPUT_KEYBOARD,
        U = new NativeMethods.InputUnion
        {
            ki = new NativeMethods.KEYBDINPUT { wVk = virtualKey, wScan = scanCode, dwFlags = flags },
        },
    };

    private static void Send(NativeMethods.INPUT input) =>
        NativeMethods.SendInput(1, [input], System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.INPUT>());
}

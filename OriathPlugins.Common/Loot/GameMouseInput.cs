namespace OriathPlugins.Common.Loot;

using System.Diagnostics;
using System.Runtime.InteropServices;
using OriathHub;
using OriathHub.Utils;

public static class GameMouseInput
{
    private const uint MouseeventfLeftdown = 0x0002;
    private const uint MouseeventfLeftup = 0x0004;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out NativePoint point);

    [DllImport("user32.dll")]
    private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    public static bool TryClick(int screenX, int screenY, int holdMs = 20, bool restoreCursor = true, int settleMs = 6)
    {
        NativePoint originalCursor = default;
        var hadCursor = false;
        if (restoreCursor)
        {
            hadCursor = GetCursorPos(out originalCursor);
        }

        if (!SetCursorPos(screenX, screenY))
        {
            return false;
        }

        if (settleMs > 0)
        {
            Thread.Sleep(Math.Clamp(settleMs, 0, 20));
        }

        var halfHold = Math.Clamp(holdMs / 2, 2, 10);
        Thread.Sleep(halfHold);
        mouse_event(MouseeventfLeftdown, 0, 0, 0, UIntPtr.Zero);
        Thread.Sleep(halfHold);
        mouse_event(MouseeventfLeftup, 0, 0, 0, UIntPtr.Zero);

        if (hadCursor)
        {
            SetCursorPos(originalCursor.X, originalCursor.Y);
        }

        if (!FocusHelper.IsGameForeground() && !Core.Process.Foreground)
        {
            TryRefocusGame();
        }

        return true;
    }

    private static void TryRefocusGame()
    {
        var pid = Core.Process.Pid;
        if (pid == 0)
        {
            return;
        }

        try
        {
            var process = Process.GetProcessById((int)pid);
            if (process.MainWindowHandle != IntPtr.Zero)
            {
                SetForegroundWindow(process.MainWindowHandle);
            }
        }
        catch (ArgumentException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }
}

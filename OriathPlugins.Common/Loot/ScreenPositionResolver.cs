namespace OriathPlugins.Common.Loot;

using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using OriathHub;
using OriathHub.RemoteObjects.Components;
using OriathHub.RemoteObjects.States;
using OriathHub.RemoteObjects.States.InGameStateObjects;

public static class ScreenPositionResolver
{
    private static IntPtr cachedWindowHandle = IntPtr.Zero;
    private static uint cachedPid;

    public static bool TryGetClientPosition(InGameState inGame, Entity entity, out Vector2 clientPosition)
    {
        clientPosition = default;
        if (!entity.TryGetComponent<Render>(out var render))
        {
            return false;
        }

        var worldData = inGame.CurrentWorldInstance;
        clientPosition = worldData.WorldToScreen(render.WorldPosition, render.TerrainHeight);
        if (IsValidClientPosition(clientPosition))
        {
            return true;
        }

        clientPosition = worldData.WorldToScreen(render.WorldPosition);
        return IsValidClientPosition(clientPosition);
    }

    public static bool TryGetBaseWorldPosition(Entity entity, out Vector2 worldCenter, out float baseHeight)
    {
        worldCenter = default;
        baseHeight = 0f;
        if (!entity.TryGetComponent<Render>(out var render))
        {
            return false;
        }

        worldCenter = new Vector2(render.GridPosition.X, render.GridPosition.Y);
        baseHeight = render.TerrainHeight;
        return true;
    }

    public static bool TryGetBaseClientPosition(InGameState inGame, Entity entity, out Vector2 clientPosition)
    {
        clientPosition = default;
        if (!entity.TryGetComponent<Render>(out var render))
        {
            return false;
        }

        var worldData = inGame.CurrentWorldInstance;
        clientPosition = worldData.WorldToScreen(render.GridPosition, render.TerrainHeight);
        if (IsValidClientPosition(clientPosition))
        {
            return true;
        }

        clientPosition = worldData.WorldToScreen(render.WorldPosition, render.TerrainHeight);
        if (IsValidClientPosition(clientPosition))
        {
            return true;
        }

        return TryGetClientPosition(inGame, entity, out clientPosition);
    }

    public static Vector2 ClientToScreen(Vector2 clientPosition)
    {
        var point = new NativePoint
        {
            X = (int)Math.Round(clientPosition.X),
            Y = (int)Math.Round(clientPosition.Y),
        };

        var windowHandle = ResolveGameWindowHandle();
        if (windowHandle != IntPtr.Zero && NativeMethods.ClientToScreen(windowHandle, ref point))
        {
            return new Vector2(point.X, point.Y);
        }

        var window = Core.Process.WindowArea;
        return new Vector2(clientPosition.X + window.X, clientPosition.Y + window.Y);
    }

    public static bool IsWithinClientArea(Vector2 clientPosition)
    {
        var window = Core.Process.WindowArea;
        if (window.Width <= 0 || window.Height <= 0)
        {
            return IsValidClientPosition(clientPosition);
        }

        const float margin = 8f;
        return clientPosition.X >= margin &&
               clientPosition.Y >= margin &&
               clientPosition.X <= window.Width - margin &&
               clientPosition.Y <= window.Height - margin;
    }

    public static bool IsWithinGameWindow(Vector2 screenPosition)
    {
        var window = Core.Process.WindowArea;
        if (window.Width <= 0 || window.Height <= 0)
        {
            return screenPosition.X > 0 && screenPosition.Y > 0;
        }

        const float margin = 8f;
        return screenPosition.X >= window.X + margin &&
               screenPosition.Y >= window.Y + margin &&
               screenPosition.X <= window.X + window.Width - margin &&
               screenPosition.Y <= window.Y + window.Height - margin;
    }

    public static bool TryGetClickableScreenPosition(Vector2 clientPosition, out Vector2 screenPosition)
    {
        screenPosition = default;
        if (!IsWithinClientArea(clientPosition))
        {
            return false;
        }

        screenPosition = ClientToScreen(clientPosition);
        return IsWithinGameWindow(screenPosition);
    }

    private static IntPtr ResolveGameWindowHandle()
    {
        var pid = Core.Process.Pid;
        if (pid == 0)
        {
            cachedWindowHandle = IntPtr.Zero;
            cachedPid = 0;
            return IntPtr.Zero;
        }

        if (pid == cachedPid && cachedWindowHandle != IntPtr.Zero)
        {
            return cachedWindowHandle;
        }

        try
        {
            var process = Process.GetProcessById((int)pid);
            cachedWindowHandle = process.MainWindowHandle;
            cachedPid = pid;
            return cachedWindowHandle;
        }
        catch (ArgumentException)
        {
            cachedWindowHandle = IntPtr.Zero;
            cachedPid = 0;
            return IntPtr.Zero;
        }
        catch (InvalidOperationException)
        {
            cachedWindowHandle = IntPtr.Zero;
            cachedPid = 0;
            return IntPtr.Zero;
        }
    }

    private static bool IsValidClientPosition(Vector2 position) =>
        !float.IsNaN(position.X) &&
        !float.IsNaN(position.Y) &&
        !float.IsInfinity(position.X) &&
        !float.IsInfinity(position.Y) &&
        position.X > 0f &&
        position.Y > 0f;

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    private static class NativeMethods
    {
        [DllImport("user32.dll")]
        public static extern bool ClientToScreen(IntPtr hWnd, ref NativePoint lpPoint);
    }
}

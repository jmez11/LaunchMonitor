using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using LaunchMonitor.Core;

namespace LaunchMonitor;

internal sealed class WindowWatcher : IDisposable
{
    private readonly LaunchHistory history = new();
    private readonly HashSet<nint> knownWindows = [];
    private readonly Dictionary<nint, PendingWindow> pendingWindows = [];
    private readonly Native.HookProc mouseCallback;
    private readonly Native.HookProc keyboardCallback;
    private readonly Native.WinEventProc windowCallback;
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 100 };
    private nint mouseHook, keyboardHook, showHook, destroyHook, moveHook;
    private bool enabled = true;
    private bool disposed;

    internal WindowWatcher()
    {
        // Keep delegate references alive for as long as their native hooks exist.
        mouseCallback = OnMouse;
        keyboardCallback = OnKeyboard;
        windowCallback = OnWindowEvent;
        Reset();
        try
        {
            var module = Native.GetModuleHandleW(null);
            mouseHook = Native.SetWindowsHookExW(Native.WhMouseLl, mouseCallback, module, 0);
            keyboardHook = Native.SetWindowsHookExW(Native.WhKeyboardLl, keyboardCallback, module, 0);
            const uint skipOwnProcess = 2; // Out-of-context callbacks run on our message-loop thread.
            showHook = Native.SetWinEventHook(Native.EventObjectShow, Native.EventObjectShow,
                0, windowCallback, 0, 0, skipOwnProcess);
            destroyHook = Native.SetWinEventHook(Native.EventObjectDestroy, Native.EventObjectDestroy,
                0, windowCallback, 0, 0, skipOwnProcess);
            moveHook = Native.SetWinEventHook(Native.EventMoveSizeStart, Native.EventMoveSizeStart,
                0, windowCallback, 0, 0, skipOwnProcess);
            if (mouseHook == 0 || keyboardHook == 0 || showHook == 0 || destroyHook == 0 || moveHook == 0)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows did not allow window/input hooks.");
            timer.Tick += OnTick;
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    internal void SetEnabled(bool value)
    {
        enabled = value;
        Reset();
    }

    internal void Reset()
    {
        timer.Stop();
        pendingWindows.Clear();
        knownWindows.Clear();
        history.Clear();
        // Include hidden windows: showing an existing window must not move it.
        Native.EnumWindows((window, _) =>
        {
            knownWindows.Add(window);
            var identity = GetProcessIdentity(window);
            if (identity is { } process)
                history.MarkExisting(process);
            return true;
        }, 0);
    }

    private nint OnMouse(int code, nint message, nint data)
    {
        if (code >= 0 && enabled && message is 0x0201 or 0x0207) // Left/middle button down.
        {
            try
            {
                var input = Marshal.PtrToStructure<Native.MouseData>(data);
                if ((input.Flags & 1) == 0) // Ignore injected input.
                    RecordInput(input.Position);
            }
            catch (Exception exception) { Log.Error(exception); }
        }
        return Native.CallNextHookEx(mouseHook, code, message, data);
    }

    private nint OnKeyboard(int code, nint message, nint data)
    {
        if (code >= 0 && enabled && message is 0x0100 or 0x0104) // Key down / system key down.
        {
            try
            {
                var input = Marshal.PtrToStructure<Native.KeyboardData>(data);
                // Remember time and monitor only; never store key values or typed text.
                if ((input.Flags & 0x10) == 0 && input.Key is not (0x10 or 0x11 or 0x12 or 0x5B or 0x5C)
                    && Native.GetCursorPos(out var point))
                    RecordInput(point);
            }
            catch (Exception exception) { Log.Error(exception); }
        }
        return Native.CallNextHookEx(keyboardHook, code, message, data);
    }

    private void RecordInput(Native.Point point)
    {
        if (Screen.AllScreens.Length < 2)
            return;
        var info = new Native.MonitorInfo { Size = Marshal.SizeOf<Native.MonitorInfo>() };
        var monitor = Native.MonitorFromPoint(point, 2);
        if (Native.GetMonitorInfoW(monitor, ref info))
            history.Record(DateTimeOffset.UtcNow, info.Device);
    }

    private void OnWindowEvent(nint hook, uint eventType, nint window, int objectId,
        int childId, uint threadId, uint time)
    {
        if (window == 0 || objectId != 0 || childId != 0)
            return;
        try
        {
            if (eventType == Native.EventObjectDestroy)
            {
                knownWindows.Remove(window);
                pendingWindows.Remove(window);
            }
            else if (eventType == Native.EventMoveSizeStart)
            {
                // Once the user starts moving/resizing this window, leave it alone.
                pendingWindows.Remove(window);
            }
            else if (eventType == Native.EventObjectShow && knownWindows.Add(window)
                && enabled && Screen.AllScreens.Length > 1)
            {
                pendingWindows[window] = new(DateTimeOffset.UtcNow);
                timer.Start();
            }
        }
        catch (Exception exception) { Log.Error(exception); }
    }

    private void OnTick(object? sender, EventArgs args)
    {
        foreach (var (window, pending) in pendingWindows.ToArray())
        {
            try
            {
                var age = DateTimeOffset.UtcNow - pending.ShownAt;
                var due = pending.Attempts switch { 0 => 150, 1 => 500, _ => 1200 };
                if (age.TotalMilliseconds < due)
                    continue;

                if (!IsApplicationWindow(window))
                {
                    pendingWindows.Remove(window);
                    continue;
                }

                if (pending.Attempts == 0)
                {
                    var process = GetProcessIdentity(window);
                    pending.Target = process is { } identity ? history.Resolve(identity, pending.ShownAt) : null;
                }

                var target = Screen.AllScreens.FirstOrDefault(screen => screen.DeviceName == pending.Target);
                if (target is null || !MoveToMonitor(window, target) || ++pending.Attempts >= 3)
                    pendingWindows.Remove(window);
            }
            catch (Exception exception)
            {
                pendingWindows.Remove(window);
                Log.Error(exception);
            }
        }
        if (pendingWindows.Count == 0)
            timer.Stop();
    }

    private static bool IsApplicationWindow(nint window)
    {
        if (!Native.IsWindow(window) || !Native.IsWindowVisible(window) || Native.IsIconic(window))
            return false;
        var style = Native.GetWindowLong(window, Native.GwlStyle);
        var extended = Native.GetWindowLong(window, Native.GwlExStyle);
        if ((style & Native.WsChild) != 0 || (extended & (Native.WsExToolWindow | Native.WsExNoActivate)) != 0
            || Native.GetWindow(window, 4) != 0) // GW_OWNER: don't relocate dialogs/popups.
            return false;
        var name = new StringBuilder(256);
        Native.GetClassNameW(window, name, name.Capacity);
        return name.ToString() is not ("Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd"
            or "#32768" or "#32770" or "tooltips_class32" or "Windows.UI.Core.CoreWindow"
            or "XamlExplorerHostIslandWindow" or "TopLevelWindowForOverflowXamlIsland");
    }

    private static ProcessIdentity? GetProcessIdentity(nint window)
    {
        Native.GetWindowThreadProcessId(window, out var id);
        if (id == 0 || id == Environment.ProcessId)
            return null;
        try
        {
            using var process = Process.GetProcessById((int)id);
            return new((int)id, new DateTimeOffset(process.StartTime.ToUniversalTime()));
        }
        catch (ArgumentException) { return null; } // Process exited between callbacks.
        catch (InvalidOperationException) { return null; }
        catch (Win32Exception) { return null; } // Protected/elevated process.
    }

    private static bool MoveToMonitor(nint window, Screen target)
    {
        var sourceHandle = Native.MonitorFromWindow(window, 2);
        var source = new Native.MonitorInfo { Size = Marshal.SizeOf<Native.MonitorInfo>() };
        if (!Native.GetMonitorInfoW(sourceHandle, ref source))
            return false;
        if (source.Device == target.DeviceName)
            return true;

        var targetWork = new DesktopRect(target.WorkingArea.X, target.WorkingArea.Y,
            target.WorkingArea.Width, target.WorkingArea.Height);
        if (Native.IsZoomed(window))
        {
            var placement = new Native.Placement { Length = Marshal.SizeOf<Native.Placement>() };
            if (!Native.GetWindowPlacement(window, ref placement))
                return false;
            // WINDOWPLACEMENT uses workspace coordinates for ordinary top-level windows.
            // Convert through screen coordinates to support taskbars on any edge.
            var normal = placement.NormalPosition.ToDesktopRect();
            normal = normal with
            {
                X = normal.X + source.Work.Left - source.Monitor.Left,
                Y = normal.Y + source.Work.Top - source.Monitor.Top
            };
            var mapped = WindowPlacement.Map(normal, source.Work.ToDesktopRect(), targetWork);
            mapped = mapped with
            {
                X = mapped.X - (target.WorkingArea.X - target.Bounds.X),
                Y = mapped.Y - (target.WorkingArea.Y - target.Bounds.Y)
            };
            placement.NormalPosition = Native.Rect.From(mapped);
            placement.Flags |= 4; // WPF_ASYNCWINDOWPLACEMENT: don't block on another app's UI thread.
            return Native.SetWindowPlacement(window, in placement);
        }

        if (!Native.GetWindowRect(window, out var rect))
            return false;
        var destination = WindowPlacement.Map(rect.ToDesktopRect(), source.Work.ToDesktopRect(), targetWork);
        return Native.SetWindowPos(window, 0, destination.X, destination.Y, destination.Width, destination.Height,
            Native.SwpNoActivate | Native.SwpNoZOrder | Native.SwpNoOwnerZOrder | Native.SwpAsyncWindowPos);
    }

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        timer.Stop();
        timer.Dispose();
        if (mouseHook != 0) Native.UnhookWindowsHookEx(mouseHook);
        if (keyboardHook != 0) Native.UnhookWindowsHookEx(keyboardHook);
        if (showHook != 0) Native.UnhookWinEvent(showHook);
        if (destroyHook != 0) Native.UnhookWinEvent(destroyHook);
        if (moveHook != 0) Native.UnhookWinEvent(moveHook);
        GC.KeepAlive(mouseCallback);
        GC.KeepAlive(keyboardCallback);
        GC.KeepAlive(windowCallback);
    }

    private sealed class PendingWindow(DateTimeOffset shownAt)
    {
        internal DateTimeOffset ShownAt { get; } = shownAt;
        internal int Attempts { get; set; }
        internal string? Target { get; set; }
    }
}

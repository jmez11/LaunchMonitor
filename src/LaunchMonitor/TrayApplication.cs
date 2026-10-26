using Microsoft.Win32;

namespace LaunchMonitor;

internal sealed class TrayApplication : ApplicationContext
{
    private const string StartupKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private readonly Control dispatcher = new();
    private readonly NotifyIcon tray;
    private readonly Icon trayIcon;
    private readonly ContextMenuStrip menu = new();
    private readonly ToolStripMenuItem pause = new("Pause") { CheckOnClick = true };
    private readonly ToolStripMenuItem startup = new("Start at sign-in") { CheckOnClick = true };
    private readonly WindowWatcher watcher;
    private bool sessionLocked;

    internal TrayApplication()
    {
        dispatcher.CreateControl();
        watcher = new WindowWatcher();
        using var appIcon = new Icon(typeof(TrayApplication), "Assets.LaunchMonitor.ico");
        trayIcon = new Icon(appIcon, SystemInformation.SmallIconSize);
        tray = new NotifyIcon
        {
            Icon = trayIcon,
            Text = "LaunchMonitor — new windows follow your launch monitor",
            ContextMenuStrip = menu,
            Visible = true
        };
        menu.Items.Add(new ToolStripMenuItem("LaunchMonitor") { Enabled = false });
        menu.Items.Add(pause);
        menu.Items.Add(startup);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("About", null, (_, _) => MessageBox.Show(
            "LaunchMonitor 0.1.0\n\nNew windows follow the monitor where you launch them.\n"
            + "Run this app inside your remote Windows session.\n\n"
            + "Portable. No administrator rights required.\n"
            + "No network connections, telemetry, or stored keystrokes.\n\n"
            + "Right-click this icon to pause or exit.", "LaunchMonitor",
            MessageBoxButtons.OK, MessageBoxIcon.Information));
        menu.Items.Add("Exit", null, (_, _) => ExitThread());
        pause.CheckedChanged += (_, _) => UpdateEnabled();
        startup.Checked = HasStartupEntry();
        startup.CheckedChanged += OnStartupChanged;
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        SystemEvents.SessionSwitch += OnSessionSwitch;
    }

    private void UpdateEnabled()
    {
        watcher.SetEnabled(!pause.Checked && !sessionLocked);
        tray.Text = pause.Checked ? "LaunchMonitor — paused" : "LaunchMonitor — active";
    }

    private static bool HasStartupEntry()
    {
        using var key = Registry.CurrentUser.OpenSubKey(StartupKey);
        return key?.GetValue("LaunchMonitor") is string;
    }

    private void OnStartupChanged(object? sender, EventArgs args)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(StartupKey, true);
            if (startup.Checked)
                key.SetValue("LaunchMonitor", $"\"{Environment.ProcessPath}\"");
            else
                key.DeleteValue("LaunchMonitor", false);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or System.Security.SecurityException
            or IOException)
        {
            Log.Error(exception);
            startup.CheckedChanged -= OnStartupChanged;
            startup.Checked = HasStartupEntry();
            startup.CheckedChanged += OnStartupChanged;
            MessageBox.Show("Windows policy prevented changing startup settings for your account.",
                "LaunchMonitor", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void Dispatch(Action action)
    {
        if (dispatcher.IsDisposed || !dispatcher.IsHandleCreated)
            return;
        try { dispatcher.BeginInvoke(action); }
        catch (InvalidOperationException) { } // Application is shutting down.
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs args) => Dispatch(watcher.Reset);

    private void OnSessionSwitch(object sender, SessionSwitchEventArgs args) => Dispatch(() =>
    {
        if (args.Reason is SessionSwitchReason.SessionLock or SessionSwitchReason.RemoteDisconnect)
            sessionLocked = true;
        else if (args.Reason is SessionSwitchReason.SessionUnlock or SessionSwitchReason.RemoteConnect)
            sessionLocked = false;
        UpdateEnabled();
    });

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
            SystemEvents.SessionSwitch -= OnSessionSwitch;
            watcher.Dispose();
            tray.Visible = false;
            tray.Dispose();
            trayIcon.Dispose();
            menu.Dispose();
            dispatcher.Dispose();
        }
        base.Dispose(disposing);
    }
}

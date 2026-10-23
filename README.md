# LaunchMonitor

A small, portable Windows tray app that moves newly opened application windows to the monitor you launched them from. Built for multi-monitor Remote Desktop sessions; it also works on a local desktop.

**No installation, administrator rights, or separately installed .NET runtime required.**

## Run it

1. Download the portable ZIP from this repository's Releases page. Use `win-x64` for ordinary Intel/AMD Windows PCs, or `win-arm64` for Windows on ARM.
2. Extract it into a folder you can write to, then run `LaunchMonitor.exe`.
3. Launch an application from your secondary monitor. Its new window should move to that monitor shortly after appearing.

For RDP, run LaunchMonitor **inside the remote Windows session**, on the computer running the applications. Enable RDP's **Use all my monitors for the remote session** option so the remote desktop sees separate monitors. Each logged-in session runs its own copy. A connection that presents one giant spanned monitor does not expose separate destinations to this app.

Right-click the tray icon to **Pause**, optionally **Start at sign-in**, or **Exit**. Startup is disabled by default. If enabled, keep the executable at the same path. To remove the portable app, turn off Start at sign-in, exit, and delete its folder. Error logs, if any, can be removed from `%LOCALAPPDATA%\LaunchMonitor`.

The app targets Windows 10/11 and Windows Server versions supported by .NET 10 with the desktop experience. Builds are unsigned; Windows may display a publisher/reputation prompt, and an organization's application policy can block running portable executables.

## How it chooses a monitor

- Mouse clicks and keyboard activity record the time and the monitor containing the mouse pointer. Moving the pointer alone does not change the recorded launch destination. For keyboard launches, leave the pointer on the desired monitor when you press the launch key.
- For the first new window of a newly started process, it uses the most recent input **before that process started**, up to 15 seconds earlier. This preserves the destination if you click elsewhere while the app loads. The process must show its window within one minute of starting.
- For a new window in an already-running process, it uses input from the three seconds before the window appeared. Windows does not expose a universal causal link between a launch action and a new window, so this case is an approximation.
- It observes new window show events, waits briefly for initialization, and makes up to three placement attempts during the first 1.2 seconds. Windows may appear on their original monitor briefly before moving.
- It leaves already-existing windows, owned dialogs, tool windows, menus, and minimized windows alone. A manual drag/resize cancels any remaining attempts for that window. It preserves window size where possible, clamps placement to the target work area, and preserves maximized state.
- Display layout changes, lock/unlock, and RDP disconnect/reconnect clear pending moves and input history so old monitor destinations aren't applied to the new session layout.

## Boundaries

This is launch correlation, not a Windows shell replacement. An app that routes a launch through another process, reuses a pre-created window, opens several independent startup windows, or sets its own position later can behave differently. Background windows appearing immediately after unrelated input may be associated with that input. The app cannot universally recover the original launch monitor for those cases.

It runs as your normal user. Windows can prevent inspecting or moving windows belonging to elevated/protected applications. No elevation is requested. Running in the local RDP client cannot control individual windows inside the remote session.

No network requests or telemetry. No key values, typed text, window titles, or application usage are recorded to disk. Input history contains only timestamps and monitor device names, is limited to 256 entries, and stays in memory. The only optional registry change is the current user's `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\LaunchMonitor` startup entry. The self-contained .NET bundle may extract runtime files to the user's temporary directory on first run.

## Develop and build

Install the .NET 10 SDK. No administrator privileges are needed to run the resulting app.

```powershell
dotnet test tests/LaunchMonitor.Core.Tests --configuration Release
dotnet run --project src/LaunchMonitor
./scripts/Publish.ps1                 # portable x64 ZIP
./scripts/Publish.ps1 -Runtime win-arm64
```

The publish script includes the .NET runtime in a single executable. It writes the executable, README, license, ZIP, and SHA-256 checksum under `artifacts/`. You can cross-compile the Windows app on Linux, but running it requires Windows.

GitHub Actions builds both architectures and runs the core tests on Windows for every main-branch push and pull request. Push a `v*` tag to publish downloadable release assets. Development builds are also available as workflow artifacts.

The x64 workflow also launches the portable executable with `--smoke-test`. That initializes the tray app and native hooks, runs its message loop briefly, and exits with a status code. It does not simulate multi-monitor launches.

The code is split into a platform-independent launch/geometry library and a Windows Forms tray app using Win32 input hooks and window events. There is no polling of all running windows: the timer only processes newly shown candidate windows. Hook callbacks never suppress input.

## Windows/RDP acceptance checks

Automated tests cover launch timing and placement geometry. Live Win32 behavior needs the following checks on a Windows multi-monitor desktop and inside an RDP session:

- Launch a fresh app from each monitor; include an app that takes a few seconds to start and move/click the pointer on another screen while it loads.
- Open a second window in an existing app, launch using Enter, and launch a maximized app.
- Check monitors to the left/above the primary, different resolutions/scaling, and a taskbar on a top/left edge.
- Drag/resize a new window immediately; verify the tool stops moving it.
- Verify dialogs, context menus, existing windows, and minimized windows stay put.
- Pause/resume; disconnect/reconnect RDP with a different monitor layout; lock/unlock; repeat a launch.
- Run two remote sessions independently. Verify ordinary apps work without elevation and protected apps fail gracefully.
- Enable sign-in startup, sign out/in, disable it, and verify the current-user startup entry is removed.

These live checks are not claimed to have passed merely because a cross-platform build or unit tests pass.

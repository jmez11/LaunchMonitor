namespace LaunchMonitor;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        var smokeTest = args.Contains("--smoke-test", StringComparer.Ordinal);
        // Local namespace isolates independent RDP sessions on a shared host.
        using var mutex = new Mutex(true, @"Local\LaunchMonitor.Tray", out var firstInstance);
        if (!firstInstance)
            return 0;

        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        var exitCode = 0;
        Application.ThreadException += (_, eventArgs) =>
        {
            Log.Error(eventArgs.Exception);
            exitCode = 1;
            Application.ExitThread();
        };
        AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
            Log.Error(eventArgs.ExceptionObject as Exception ?? new Exception("Unexpected application error."));

        try
        {
            using var app = new TrayApplication();
            using var smokeTimer = new System.Windows.Forms.Timer { Interval = 750 };
            if (smokeTest)
            {
                smokeTimer.Tick += (_, _) => app.ExitThread();
                smokeTimer.Start();
            }
            Application.Run(app);
            return exitCode;
        }
        catch (Exception exception)
        {
            Log.Error(exception);
            if (!smokeTest)
                MessageBox.Show($"LaunchMonitor could not start.\n\n{exception.Message}", "LaunchMonitor",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }
}

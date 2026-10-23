namespace LaunchMonitor;

internal static class Log
{
    internal static string DirectoryPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LaunchMonitor");

    internal static void Error(Exception exception)
    {
        try
        {
            Directory.CreateDirectory(DirectoryPath);
            var path = Path.Combine(DirectoryPath, "errors.log");
            if (File.Exists(path) && new FileInfo(path).Length > 256 * 1024)
                File.Move(path, path + ".old", true);
            File.AppendAllText(path, $"{DateTimeOffset.UtcNow:O} {exception}\n");
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}

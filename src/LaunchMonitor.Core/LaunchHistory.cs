namespace LaunchMonitor.Core;

/// <summary>Correlates a new window with recent input without storing keys or window titles.</summary>
public sealed class LaunchHistory
{
    private readonly List<Interaction> interactions = [];
    private readonly HashSet<ProcessIdentity> seenProcesses = [];
    private static readonly TimeSpan Retention = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan LaunchLookback = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan ExistingProcessLookback = TimeSpan.FromSeconds(3);

    public void Record(DateTimeOffset at, string monitor)
    {
        interactions.RemoveAll(input => at - input.At > Retention);
        interactions.Add(new(at, monitor));
        if (interactions.Count > 256)
            interactions.RemoveAt(0);
    }

    public void MarkExisting(ProcessIdentity process) => seenProcesses.Add(process);

    public string? Resolve(ProcessIdentity process, DateTimeOffset windowShownAt)
    {
        var firstWindow = seenProcesses.Add(process);
        var age = windowShownAt - process.StartedAt;
        if (firstWindow && age >= TimeSpan.Zero && age <= TimeSpan.FromMinutes(1))
        {
            // Freeze the launch monitor at process creation, even if the user clicks
            // on another screen while the application is loading.
            return FindBefore(process.StartedAt, LaunchLookback);
        }

        // A running process can create new windows without creating a new process.
        // Windows provides no general causal link here; use very recent input.
        return FindBefore(windowShownAt, ExistingProcessLookback);
    }

    public void Forget(ProcessIdentity process) => seenProcesses.Remove(process);

    public void Clear()
    {
        interactions.Clear();
        seenProcesses.Clear();
    }

    private string? FindBefore(DateTimeOffset at, TimeSpan lookback) => interactions
        .Where(input => input.At <= at && at - input.At <= lookback)
        .MaxBy(input => input.At)?.Monitor;

    private sealed record Interaction(DateTimeOffset At, string Monitor);
}

public readonly record struct ProcessIdentity(int Id, DateTimeOffset StartedAt);

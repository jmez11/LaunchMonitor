using LaunchMonitor.Core;
using Xunit;

namespace LaunchMonitor.Core.Tests;

public sealed class LaunchHistoryTests
{
    private static readonly DateTimeOffset Epoch = new(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void SlowAppUsesInputBeforeProcessStartEvenAfterClickOnAnotherScreen()
    {
        var history = new LaunchHistory();
        history.Record(Epoch, "left");
        history.Record(Epoch.AddSeconds(2), "right");
        var result = history.Resolve(new(42, Epoch.AddMilliseconds(100)), Epoch.AddSeconds(10));
        Assert.Equal("left", result);
    }

    [Fact]
    public void MostRecentInputBeforeProcessStartWins()
    {
        var history = new LaunchHistory();
        history.Record(Epoch, "left");
        history.Record(Epoch.AddSeconds(1), "right");
        Assert.Equal("right", history.Resolve(new(42, Epoch.AddSeconds(2)), Epoch.AddSeconds(3)));
    }

    [Fact]
    public void UncorrelatedNewProcessDoesNotStealLaterInput()
    {
        var history = new LaunchHistory();
        history.Record(Epoch.AddSeconds(2), "right");
        Assert.Null(history.Resolve(new(42, Epoch), Epoch.AddSeconds(3)));
    }

    [Fact]
    public void OldInputDoesNotMoveBackgroundLaunchedApplication()
    {
        var history = new LaunchHistory();
        history.Record(Epoch, "left");
        Assert.Null(history.Resolve(new(42, Epoch.AddSeconds(16)), Epoch.AddSeconds(17)));
    }

    [Fact]
    public void ExistingProcessUsesRecentInputForNewWindow()
    {
        var history = new LaunchHistory();
        var process = new ProcessIdentity(42, Epoch);
        history.MarkExisting(process);
        history.Record(Epoch.AddSeconds(10), "right");
        Assert.Equal("right", history.Resolve(process, Epoch.AddSeconds(11)));
    }

    [Fact]
    public void SecondWindowInSameProcessCanOpenOnDifferentMonitor()
    {
        var history = new LaunchHistory();
        var process = new ProcessIdentity(42, Epoch.AddMilliseconds(100));
        history.Record(Epoch, "left");
        Assert.Equal("left", history.Resolve(process, Epoch.AddSeconds(1)));
        history.Record(Epoch.AddSeconds(2), "right");
        Assert.Equal("right", history.Resolve(process, Epoch.AddSeconds(3)));
    }

    [Fact]
    public void ExistingProcessWithoutRecentInputIsLeftAlone()
    {
        var history = new LaunchHistory();
        var process = new ProcessIdentity(42, Epoch);
        history.MarkExisting(process);
        history.Record(Epoch, "left");
        Assert.Null(history.Resolve(process, Epoch.AddSeconds(4)));
    }

    [Fact]
    public void ReusedProcessIdIsTreatedAsNewProcess()
    {
        var history = new LaunchHistory();
        history.MarkExisting(new(42, Epoch.AddHours(-1)));
        history.Record(Epoch, "left");
        history.Record(Epoch.AddSeconds(3), "right");
        Assert.Equal("left", history.Resolve(new(42, Epoch.AddSeconds(1)), Epoch.AddSeconds(4)));
    }

    [Fact]
    public void ResetDropsLaunchesFromOldRdpLayout()
    {
        var history = new LaunchHistory();
        history.Record(Epoch, "disconnected-screen");
        history.Clear();
        Assert.Null(history.Resolve(new(42, Epoch.AddSeconds(1)), Epoch.AddSeconds(2)));
    }

    [Fact]
    public void RecentWindowInLongRunningProcessUsesRecentInput()
    {
        var history = new LaunchHistory();
        history.Record(Epoch.AddSeconds(65), "right");
        Assert.Equal("right", history.Resolve(new(42, Epoch), Epoch.AddSeconds(66)));
    }
}

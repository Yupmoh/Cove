using Cove.Engine.Restart;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class RestoreChooserTests
{
    private sealed class NoOpLogger : ILogger
    {
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NoOpDisposable.Instance;
        public bool IsEnabled(LogLevel logLevel) => false;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
        private sealed class NoOpDisposable : IDisposable { public static readonly NoOpDisposable Instance = new(); public void Dispose() { } }
    }

    private static string NewDir() => Path.Combine(Path.GetTempPath(), "cove-chooser-" + Guid.NewGuid().ToString("N"));

    private static RestorationService NewService(string dir, bool cleanShutdown = false)
    {
        Directory.CreateDirectory(dir);
        var svc = new RestorationService(dir, new NoOpLogger());
        if (cleanShutdown)
            svc.MarkCleanShutdown();
        else
            svc.MarkLaunching();
        return svc;
    }

    private static RestoreChoiceItem Item(string nookId, bool running, bool hidden = false, string ws = "ws-1") =>
        new(ws, "shore-1", nookId, nookId, running, hidden);

    [Fact]
    public void CleanShutdown_AutoRelaunches_AllNooks()
    {
        var dir = NewDir();
        try
        {
            var rest = NewService(dir, cleanShutdown: true);
            var chooser = new RestoreChooserService(rest);
            var nooks = new List<RestoreChoiceItem> { Item("p1", true), Item("p2", false) };

            var result = chooser.Evaluate(nooks);

            Assert.True(result.AutoRelaunch);
            Assert.Equal(2, result.Items.Count);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void UncleanExit_AutoRestoreOnLaunch_SkipsChooser()
    {
        var dir = NewDir();
        try
        {
            var rest = NewService(dir, cleanShutdown: false);
            var chooser = new RestoreChooserService(rest);
            chooser.SaveSettings(new RestoreSettings(true));
            var nooks = new List<RestoreChoiceItem> { Item("p1", true), Item("p2", false) };

            var result = chooser.Evaluate(nooks);

            Assert.True(result.AutoRelaunch);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void UncleanExit_NoAutoRestore_ShowsChooser_AllNooksWithIndicators()
    {
        var dir = NewDir();
        try
        {
            var rest = NewService(dir, cleanShutdown: false);
            var chooser = new RestoreChooserService(rest);
            var nooks = new List<RestoreChoiceItem>
            {
                Item("p1", running: true),
                Item("p2", running: false),
                Item("p3", running: true, hidden: true),
            };

            var result = chooser.Evaluate(nooks);

            Assert.False(result.AutoRelaunch);
            Assert.Equal(3, result.Items.Count);
            Assert.Contains(result.Items, i => i.NookId == "p1" && i.WasRunning);
            Assert.Contains(result.Items, i => i.NookId == "p2" && !i.WasRunning);
            Assert.Contains(result.Items, i => i.NookId == "p3" && i.Hidden);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }
    [Fact]
    public void SaveLoadSettings_RoundTrips()
    {
        var dir = NewDir();
        try
        {
            var rest = NewService(dir, cleanShutdown: true);
            var chooser = new RestoreChooserService(rest);

            Assert.False(chooser.LoadSettings().AutoRestoreOnLaunch);

            chooser.SaveSettings(new RestoreSettings(true));
            Assert.True(chooser.LoadSettings().AutoRestoreOnLaunch);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void LazyMount_TracksMountedNooks()
    {
        var registry = new LazyMountRegistry();
        Assert.False(registry.IsMounted("p1"));

        registry.Mount("p1");
        Assert.True(registry.IsMounted("p1"));
        Assert.Single(registry.MountedNooks());

        registry.Unmount("p1");
        Assert.False(registry.IsMounted("p1"));
        Assert.Empty(registry.MountedNooks());
    }

    [Fact]
    public void LazyMount_UnmountNonexistent_IsNoOp()
    {
        var registry = new LazyMountRegistry();
        registry.Unmount("never-mounted");
        Assert.Empty(registry.MountedNooks());
    }
}

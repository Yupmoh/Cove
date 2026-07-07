using Cove.Engine.Restart;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class RestorationTests
{
    private sealed class NoOpLogger : ILogger
    {
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NoOpDisposable.Instance;
        public bool IsEnabled(LogLevel logLevel) => false;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
        private sealed class NoOpDisposable : IDisposable { public static readonly NoOpDisposable Instance = new(); public void Dispose() { } }
    }

    private static string NewDir() => Path.Combine(Path.GetTempPath(), "cove-restore-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void CleanShutdown_Marker_Lifecycle()
    {
        var dir = NewDir();
        Directory.CreateDirectory(dir);
        try
        {
            var svc = new RestorationService(dir, new NoOpLogger());

            Assert.False(svc.WasCleanShutdown());

            svc.MarkLaunching();
            Assert.False(svc.WasCleanShutdown());

            svc.MarkCleanShutdown();
            Assert.True(svc.WasCleanShutdown());

            svc.MarkLaunching();
            Assert.False(svc.WasCleanShutdown());
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    [Fact]
    public void LoadState_ReturnsDefault_WhenNoStateFile()
    {
        var dir = NewDir();
        Directory.CreateDirectory(dir);
        try
        {
            var svc = new RestorationService(dir, new NoOpLogger());
            var state = svc.LoadState();
            Assert.Equal(1, state.SchemaVersion);
            Assert.Empty(state.OpenWorkspaces);
            Assert.Null(state.FocusedWorkspace);
            Assert.False(state.CleanShutdown);
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    [Fact]
    public void SaveState_PersistsAndReloads()
    {
        var dir = NewDir();
        Directory.CreateDirectory(dir);
        try
        {
            var svc = new RestorationService(dir, new NoOpLogger());
            svc.SaveState(new Cove.Persistence.CoveState
            {
                OpenWorkspaces = ["ws-1", "ws-2"],
                FocusedWorkspace = "ws-2",
                CleanShutdown = true,
                ShutdownAtUtc = DateTimeOffset.UtcNow,
            });

            var loaded = svc.LoadState();
            Assert.Equal(2, loaded.OpenWorkspaces.Count);
            Assert.Equal("ws-2", loaded.FocusedWorkspace);
            Assert.True(loaded.CleanShutdown);
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    [Fact]
    public void EmitProgress_InvokesCallback()
    {
        var dir = NewDir();
        Directory.CreateDirectory(dir);
        var events = new List<RestoreProgressEvent>();
        try
        {
            var svc = new RestorationService(dir, new NoOpLogger(), emitProgress: e => events.Add(e));
            svc.EmitProgress("ws-1", "load", RestorePhase.Started, "clean");
            svc.EmitProgress("ws-1", "done", RestorePhase.Completed);

            Assert.Equal(2, events.Count);
            Assert.Equal(RestorePhase.Started, events[0].Phase);
            Assert.Equal("clean", events[0].Detail);
            Assert.Equal(RestorePhase.Completed, events[1].Phase);
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }
}

using Cove.Engine.Bays;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class RunCommandTests
{
    private sealed class FakeSessionFactory : IRunCommandSessionFactory
    {
        public IRunCommandSession Create(RunCommandDefinition def, Action<byte[]> onOutput)
            => new FakeSession(def.Id);
    }

    private sealed class FakeSession : IRunCommandSession
    {
        private static int _counter;
        public string SessionId { get; }
        public bool IsRunning { get; private set; }
        public int? ExitCode { get; private set; }

        public FakeSession(string defId)
        {
            SessionId = $"sess-{defId}-{System.Threading.Interlocked.Increment(ref _counter)}";
        }

        public void Start() => IsRunning = true;
        public void Stop() { IsRunning = false; ExitCode = 0; }
        public void Dispose() => IsRunning = false;
    }

    private static RunCommandService NewService() => new(new InMemoryRunCommandStore(), new FakeSessionFactory());

    private static RunCommandDefinition Def(string id, string label, string command, string? cwd = null) =>
        new() { Id = id, BayId = "ws-1", Label = label, Command = command, Cwd = cwd ?? "" };

    [Fact]
    public async Task Create_List_Edit_Delete_RoundTrips()
    {
        await using var svc = NewService();
        var d = await svc.CreateAsync("ws-1", "server", "vite", null);
        Assert.Equal("ws-1", d.BayId);
        Assert.Equal("server", d.Label);

        var list = await svc.ListAsync("ws-1");
        Assert.Single(list);
        Assert.Equal("server", list[0].Label);

        var edited = await svc.EditAsync(d.Id, "dev-server", "vite --host", "/app");
        Assert.Equal("dev-server", edited!.Label);
        Assert.Equal("vite --host", edited.Command);
        Assert.Equal("/app", edited.Cwd);

        var deleted = await svc.DeleteAsync(d.Id);
        Assert.True(deleted);
        Assert.Empty(await svc.ListAsync("ws-1"));
    }

    [Fact]
    public async Task Start_Is_Idempotent_AlreadyRunning_ReturnsSameState()
    {
        await using var svc = NewService();
        var d = await svc.CreateAsync("ws-1", "server", "sleep 30", null);

        var s1 = await svc.StartAsync(d.Id);
        Assert.Equal(RunCommandLifecycle.Running, s1!.Lifecycle);

        var s2 = await svc.StartAsync(d.Id);
        Assert.Equal(RunCommandLifecycle.Running, s2!.Lifecycle);
        Assert.Equal(s1.SessionId, s2.SessionId);
    }

    [Fact]
    public async Task Restart_InPlace_Revives_DeadShell_PreservingScrollback()
    {
        await using var svc = NewService();
        var d = await svc.CreateAsync("ws-1", "build", "echo hello", null);

        var started = await svc.StartAsync(d.Id);
        await svc.AppendLogAsync(d.Id, "line 1\nline 2\n"u8.ToArray());
        var beforeLogs = await svc.LogsAsync(d.Id);
        Assert.Contains("line 1", beforeLogs);

        await svc.StopAsync(d.Id);
        var stopped = await svc.StatusAsync(d.Id);
        Assert.Equal(RunCommandLifecycle.Stopped, stopped!.Lifecycle);

        var restarted = await svc.RestartAsync(d.Id);
        Assert.Equal(RunCommandLifecycle.Running, restarted!.Lifecycle);
        Assert.NotEqual(started!.SessionId, restarted.SessionId);

        var afterLogs = await svc.LogsAsync(d.Id);
        Assert.Contains("line 1", afterLogs);
    }

    [Fact]
    public async Task List_Inherits_Parent_Worktree_Commands()
    {
        await using var svc = NewService();
        await svc.CreateAsync("parent", "watcher", "npm run watch", null);
        await svc.CreateAsync("parent", "linter", "eslint .", null);
        await svc.CreateAsync("child", "tester", "vitest", null);

        var effective = await svc.ListEffectiveAsync("child", "parent");
        Assert.Equal(3, effective.Count);
        Assert.Contains(effective, d => d.Definition.Label == "watcher" && d.Inherited);
        Assert.Contains(effective, d => d.Definition.Label == "linter" && d.Inherited);
        Assert.Contains(effective, d => d.Definition.Label == "tester" && !d.Inherited);
    }

    [Fact]
    public async Task Clear_Wipes_RingBuffer()
    {
        await using var svc = NewService();
        var d = await svc.CreateAsync("ws-1", "server", "vite", null);
        await svc.StartAsync(d.Id);
        await svc.AppendLogAsync(d.Id, "noise\n"u8.ToArray());
        Assert.NotEmpty((await svc.LogsAsync(d.Id)));

        await svc.ClearAsync(d.Id);
        Assert.Empty((await svc.LogsAsync(d.Id)));
    }

    [Fact]
    public async Task Start_UnknownCommand_ReturnsNull()
    {
        await using var svc = NewService();
        var s = await svc.StartAsync("no-such-id");
        Assert.Null(s);
    }

    [Fact]
    public async Task RunningSet_PersistsAndRelaunches_OnRestart()
    {
        var dir = Path.Combine(Path.GetTempPath(), "cove-rc-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var store = new RunCommandStore(dir);
            RunCommandService svc1 = new(store, new FakeSessionFactory());
            var d = await svc1.CreateAsync("ws", "watcher", "sleep 30", null);
            await svc1.StartAsync(d.Id);
            await svc1.DisposeAsync();

            var runningJson = Path.Combine(dir, "running.json");
            Assert.True(File.Exists(runningJson));
            var persisted = System.Text.Json.JsonSerializer.Deserialize<List<string>>(
                await File.ReadAllTextAsync(runningJson), Cove.Protocol.CoveJsonContext.Default.ListString);
            Assert.Contains(d.Id, persisted!);

            RunCommandService svc2 = new(store, new FakeSessionFactory());
            await svc2.RelaunchPreviouslyRunningAsync();
            var status = await svc2.StatusAsync(d.Id);
            Assert.Equal(Cove.Engine.Bays.RunCommandLifecycle.Running, status!.Lifecycle);
            await svc2.DisposeAsync();
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }
}

internal sealed class InMemoryRunCommandStore : IRunCommandStore
{

    private readonly Dictionary<string, RunCommandDefinition> _defs = new(StringComparer.Ordinal);
    private readonly object _gate = new();

    public Task<RunCommandDefinition?> GetAsync(string id)
    {
        lock (_gate)
            return Task.FromResult(_defs.TryGetValue(id, out var d) ? d : null);
    }

    public Task<IReadOnlyList<RunCommandDefinition>> ListAsync(string bayId)
    {
        lock (_gate)
            return Task.FromResult<IReadOnlyList<RunCommandDefinition>>(
                _defs.Values.Where(d => d.BayId == bayId).ToList());
    }

    public Task<RunCommandDefinition> SaveAsync(RunCommandDefinition def)
    {
        lock (_gate)
            _defs[def.Id] = def;
        return Task.FromResult(def);
    }

    public Task<bool> DeleteAsync(string id)
    {
        lock (_gate)
            return Task.FromResult(_defs.Remove(id));
    }
}

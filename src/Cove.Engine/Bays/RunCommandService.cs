using Microsoft.Extensions.Logging;

namespace Cove.Engine.Bays;

public interface IRunCommandSessionFactory
{
    IRunCommandSession Create(RunCommandDefinition def, Action<byte[]> onOutput);
}

public interface IRunCommandSession : IDisposable
{
    string SessionId { get; }
    bool IsRunning { get; }
    int? ExitCode { get; }
    void Start();
    void Stop();
}

public sealed class RunCommandService : IAsyncDisposable
{
    private readonly IRunCommandStore _store;
    private readonly IRunCommandSessionFactory? _sessionFactory;
    private readonly Func<string> _newId;
    private readonly Microsoft.Extensions.Logging.ILogger? _logger;
    private readonly Dictionary<string, RunCommandRuntime> _runtime = new(StringComparer.Ordinal);
    private readonly object _gate = new();

    public RunCommandService(IRunCommandStore store, IRunCommandSessionFactory? sessionFactory = null, Func<string>? newId = null, Microsoft.Extensions.Logging.ILogger? logger = null)
    {
        _store = store;
        _sessionFactory = sessionFactory;
        _newId = newId ?? (() => "rc-" + Guid.NewGuid().ToString("N"));
        _logger = logger;
    }

    public async Task<RunCommandDefinition> CreateAsync(string bayId, string label, string command, string? cwd)
    {
        var def = new RunCommandDefinition
        {
            Id = _newId(),
            BayId = bayId,
            Label = label,
            Command = command,
            Cwd = cwd ?? "",
        };
        return await _store.SaveAsync(def).ConfigureAwait(false);
    }

    public async Task<RunCommandDefinition?> EditAsync(string id, string label, string command, string? cwd)
    {
        var existing = await _store.GetAsync(id).ConfigureAwait(false);
        if (existing is null)
            return null;
        var edited = existing with { Label = label, Command = command, Cwd = cwd ?? existing.Cwd };
        return await _store.SaveAsync(edited).ConfigureAwait(false);
    }

    public async Task<bool> DeleteAsync(string id)
    {
        var runtime = TryGetRuntime(id);
        if (runtime is not null)
        {
            runtime.Session?.Dispose();
            RemoveRuntime(id);
        }
        return await _store.DeleteAsync(id).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<RunCommandDefinition>> ListAsync(string bayId)
        => await _store.ListAsync(bayId).ConfigureAwait(false);

    public async Task<IReadOnlyList<RunCommandListItem>> ListEffectiveAsync(string bayId, string? parentBayId)
    {
        var own = await _store.ListAsync(bayId).ConfigureAwait(false);
        var inherited = parentBayId is not null && parentBayId != bayId
            ? await _store.ListAsync(parentBayId).ConfigureAwait(false)
            : [];
        var result = new List<RunCommandListItem>();
        foreach (var d in own)
            result.Add(new RunCommandListItem { Definition = d, Lifecycle = GetLifecycle(d.Id), Inherited = false });
        foreach (var d in inherited)
            result.Add(new RunCommandListItem { Definition = d, Lifecycle = GetLifecycle(d.Id), Inherited = true });
        return result;
    }

    public async Task<RunCommandStatus?> StatusAsync(string id)
    {
        var def = await _store.GetAsync(id).ConfigureAwait(false);
        if (def is null)
            return null;
        var runtime = TryGetRuntime(id);
        return new RunCommandStatus
        {
            Id = id,
            Lifecycle = runtime?.Lifecycle ?? RunCommandLifecycle.NotLaunched,
            SessionId = runtime?.Session?.SessionId ?? "",
            ExitCode = runtime?.Session?.ExitCode,
            StartedAtUtc = runtime?.StartedAtUtc,
            StoppedAtUtc = runtime?.StoppedAtUtc,
        };
    }

    public async Task<RunCommandStatus?> StartAsync(string id)
    {
        var def = await _store.GetAsync(id).ConfigureAwait(false);
        if (def is null)
            return null;

        lock (_gate)
        {
            if (_runtime.TryGetValue(id, out var existing) && existing.Lifecycle == RunCommandLifecycle.Running)
                return ToStatus(id, existing);
        }

        var session = CreateSession(def);
        session.Start();
        var runtime = new RunCommandRuntime
        {
            SessionId = session.SessionId,
            Session = session,
            Lifecycle = RunCommandLifecycle.Running,
            StartedAtUtc = DateTimeOffset.UtcNow,
        };
        lock (_gate)
            _runtime[id] = runtime;
        PersistRunningSet();
        return ToStatus(id, runtime);
    }

    public async Task<RunCommandStatus?> StopAsync(string id)
    {
        var def = await _store.GetAsync(id).ConfigureAwait(false);
        if (def is null)
            return null;
        var runtime = TryGetRuntime(id);
        if (runtime is null || runtime.Lifecycle != RunCommandLifecycle.Running)
            return await StatusAsync(id).ConfigureAwait(false);

        runtime.Session!.Stop();
        runtime.Lifecycle = RunCommandLifecycle.Stopped;
        runtime.StoppedAtUtc = DateTimeOffset.UtcNow;
        PersistRunningSet();
        return ToStatus(id, runtime);
    }

    public async Task<RunCommandStatus?> RestartAsync(string id)
    {
        var def = await _store.GetAsync(id).ConfigureAwait(false);
        if (def is null)
            return null;

        var runtime = TryGetRuntime(id);
        var priorScrollback = runtime?.Ring?.ToArray();

        if (runtime?.Session is { } session)
        {
            if (runtime.Lifecycle == RunCommandLifecycle.Running)
                session.Stop();
            session.Dispose();
        }

        var newSession = CreateSession(def, priorScrollback);
        newSession.Start();
        var restarted = new RunCommandRuntime
        {
            SessionId = newSession.SessionId,
            Session = newSession,
            Lifecycle = RunCommandLifecycle.Running,
            StartedAtUtc = DateTimeOffset.UtcNow,
            Ring = runtime?.Ring,
        };
        if (restarted.Ring is null && priorScrollback is { Length: > 0 })
        {
            restarted.Ring = new MemoryRingBuffer();
            restarted.Ring.Append(priorScrollback);
        }
        lock (_gate)
            _runtime[id] = restarted;
        PersistRunningSet();
        return ToStatus(id, restarted);
    }

    public async Task<string> LogsAsync(string id)
    {
        var def = await _store.GetAsync(id).ConfigureAwait(false);
        if (def is null)
            return "";
        var runtime = TryGetRuntime(id);
        return runtime?.Ring?.ToString() ?? "";
    }

    public Task AppendLogAsync(string id, byte[] data)
    {
        var runtime = TryGetRuntime(id);
        runtime?.Ring?.Append(data);
        return Task.CompletedTask;
    }

    public async Task<bool> ClearAsync(string id)
    {
        var def = await _store.GetAsync(id).ConfigureAwait(false);
        if (def is null)
            return false;
        var runtime = TryGetRuntime(id);
        runtime?.Ring?.Clear();
        return true;
    }

    private IRunCommandSession CreateSession(RunCommandDefinition def, byte[]? priorScrollback = null)
    {
        if (_sessionFactory is not { } factory)
            throw new InvalidOperationException("no session factory configured");
        return factory.Create(def, bytes =>
        {
            var runtime = TryGetRuntime(def.Id);
            runtime?.Ring?.Append(bytes);
        });
    }

    private RunCommandRuntime? TryGetRuntime(string id)
    {
        lock (_gate)
            return _runtime.TryGetValue(id, out var r) ? r : null;
    }

    private void RemoveRuntime(string id)
    {
        lock (_gate)
            _runtime.Remove(id);
    }

    private RunCommandLifecycle GetLifecycle(string id)
    {
        var runtime = TryGetRuntime(id);
        return runtime?.Lifecycle ?? RunCommandLifecycle.NotLaunched;
    }

    private static RunCommandStatus ToStatus(string id, RunCommandRuntime runtime) => new()
    {
        Id = id,
        Lifecycle = runtime.Lifecycle,
        SessionId = runtime.Session?.SessionId ?? runtime.SessionId,
        ExitCode = runtime.Session?.ExitCode,
        StartedAtUtc = runtime.StartedAtUtc,
        StoppedAtUtc = runtime.StoppedAtUtc,
    };

    public async ValueTask DisposeAsync()
    {
        PersistRunningSet();
        List<RunCommandRuntime> runtimes;
        lock (_gate)
        {
            runtimes = _runtime.Values.ToList();
            _runtime.Clear();
        }
        foreach (var r in runtimes)
            r.Session?.Dispose();
    }

    public async Task RelaunchPreviouslyRunningAsync()
    {
        var running = LoadRunningSet();
        foreach (var id in running)
        {
            var def = await _store.GetAsync(id).ConfigureAwait(false);
            if (def is null)
                continue;
            try { await StartAsync(id).ConfigureAwait(false); }
            catch (System.Exception ex) { _logger?.LogWarning(ex, "run-command relaunch failed for {Id}", id); }
        }
    }

    private void PersistRunningSet()
    {
        try
        {
            List<string> running;
            lock (_gate)
                running = _runtime.Where(kv => kv.Value.Lifecycle == RunCommandLifecycle.Running).Select(kv => kv.Key).ToList();
            var path = System.IO.Path.Combine(_store is RunCommandStore rs ? rs.Dir : ".", "running.json");
            Cove.Persistence.AtomicJsonStore.WriteRawText(path, System.Text.Json.JsonSerializer.Serialize(running, Cove.Protocol.CoveJsonContext.Default.ListString));
        }
        catch (System.Exception ex) { _logger?.LogWarning(ex, "run-command running-set persist failed"); }
    }

    private List<string> LoadRunningSet()
    {
        try
        {
            var path = System.IO.Path.Combine(_store is RunCommandStore rs ? rs.Dir : ".", "running.json");
            if (!System.IO.File.Exists(path))
                return new List<string>();
            return System.Text.Json.JsonSerializer.Deserialize(System.IO.File.ReadAllText(path), Cove.Protocol.CoveJsonContext.Default.ListString) ?? new List<string>();
        }
        catch (System.Exception ex) { _logger?.LogWarning(ex, "run-command running-set load failed"); return new List<string>(); }
    }
    private sealed class RunCommandRuntime
    {
        public string SessionId { get; init; } = "";
        public IRunCommandSession? Session { get; set; }
        public RunCommandLifecycle Lifecycle { get; set; } = RunCommandLifecycle.NotLaunched;
        public DateTimeOffset? StartedAtUtc { get; set; }
        public DateTimeOffset? StoppedAtUtc { get; set; }
        public MemoryRingBuffer? Ring { get; set; } = new();
    }
}

public sealed class MemoryRingBuffer
{
    private readonly System.Text.StringBuilder _sb = new();
    private readonly object _gate = new();

    public void Append(byte[] data)
    {
        lock (_gate)
            _sb.Append(System.Text.Encoding.UTF8.GetString(data));
    }

    public void Clear()
    {
        lock (_gate)
            _sb.Clear();
    }

    public byte[] ToArray()
    {
        lock (_gate)
            return System.Text.Encoding.UTF8.GetBytes(_sb.ToString());
    }

    public override string ToString()
    {
        lock (_gate)
            return _sb.ToString();
    }
}

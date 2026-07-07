namespace Cove.Engine.Workspaces;

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
    private readonly Dictionary<string, RunCommandRuntime> _runtime = new(StringComparer.Ordinal);
    private readonly object _gate = new();

    public RunCommandService(IRunCommandStore store, IRunCommandSessionFactory? sessionFactory = null, Func<string>? newId = null)
    {
        _store = store;
        _sessionFactory = sessionFactory;
        _newId = newId ?? (() => "rc-" + Guid.NewGuid().ToString("N"));
    }

    public async Task<RunCommandDefinition> CreateAsync(string workspaceId, string label, string command, string? cwd)
    {
        var def = new RunCommandDefinition
        {
            Id = _newId(),
            WorkspaceId = workspaceId,
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

    public async Task<IReadOnlyList<RunCommandDefinition>> ListAsync(string workspaceId)
        => await _store.ListAsync(workspaceId).ConfigureAwait(false);

    public async Task<IReadOnlyList<RunCommandListItem>> ListEffectiveAsync(string workspaceId, string? parentWorkspaceId)
    {
        var own = await _store.ListAsync(workspaceId).ConfigureAwait(false);
        var inherited = parentWorkspaceId is not null && parentWorkspaceId != workspaceId
            ? await _store.ListAsync(parentWorkspaceId).ConfigureAwait(false)
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
        List<RunCommandRuntime> runtimes;
        lock (_gate)
        {
            runtimes = _runtime.Values.ToList();
            _runtime.Clear();
        }
        foreach (var r in runtimes)
            r.Session?.Dispose();
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

using Cove.Persistence;
using Microsoft.Extensions.Logging;

namespace Cove.Engine.Workspaces;

public sealed class RunCommandStore : IRunCommandStore
{
    private readonly string _dir;
    private readonly ILogger? _logger;

    public RunCommandStore(string dir, ILogger? logger = null)
    {
        _dir = dir;
        _logger = logger;
    }

    public Task<RunCommandDefinition?> GetAsync(string id)
    {
        var path = PathFor(id);
        if (!File.Exists(path))
            return Task.FromResult<RunCommandDefinition?>(null);
        return Task.FromResult(AtomicJsonStore.Read(path, RunCommandJsonContext.Default.RunCommandDefinition, _logger ?? NoOpLogger.Instance));
    }

    public Task<IReadOnlyList<RunCommandDefinition>> ListAsync(string workspaceId)
    {
        var result = new List<RunCommandDefinition>();
        if (!Directory.Exists(_dir))
            return Task.FromResult<IReadOnlyList<RunCommandDefinition>>(result);

        foreach (var file in Directory.EnumerateFiles(_dir, "*.json"))
        {
            var def = AtomicJsonStore.Read(file, RunCommandJsonContext.Default.RunCommandDefinition, _logger ?? NoOpLogger.Instance);
            if (def is { } d && d.WorkspaceId == workspaceId)
                result.Add(d);
        }
        return Task.FromResult<IReadOnlyList<RunCommandDefinition>>(result);
    }

    public Task<RunCommandDefinition> SaveAsync(RunCommandDefinition def)
    {
        AtomicJsonStore.Write(PathFor(def.Id), def, RunCommandJsonContext.Default.RunCommandDefinition);
        return Task.FromResult(def);
    }

    public Task<bool> DeleteAsync(string id)
    {
        var path = PathFor(id);
        if (!File.Exists(path))
            return Task.FromResult(false);
        try { File.Delete(path); return Task.FromResult(true); }
        catch (IOException) { return Task.FromResult(false); }
    }

    private string PathFor(string id) => Path.Combine(_dir, id + ".json");

    private sealed class NoOpLogger : ILogger
    {
        public static readonly NoOpLogger Instance = new();
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NoOpDisposable.Instance;
        public bool IsEnabled(LogLevel logLevel) => false;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
        private sealed class NoOpDisposable : IDisposable
        {
            public static readonly NoOpDisposable Instance = new();
            public void Dispose() { }
        }
    }
}

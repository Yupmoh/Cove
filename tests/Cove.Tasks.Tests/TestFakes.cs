using Cove.Tasks.Contracts;
using Cove.Persistence;
using Cove.Tasks.Store;
using Cove.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Tasks.Tests;

public abstract class TasksTestBase : IAsyncLifetime
{
    private static readonly System.TimeSpan DefaultLifecycleTimeout = System.TimeSpan.FromSeconds(5);
    private readonly List<IAsyncDisposable> _resources = [];
    private readonly System.TimeSpan _lifecycleTimeout;

    protected TasksTestBase()
        : this(DefaultLifecycleTimeout)
    {
    }

    protected TasksTestBase(System.TimeSpan lifecycleTimeout)
    {
        if (lifecycleTimeout <= System.TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(lifecycleTimeout));
        _lifecycleTimeout = lifecycleTimeout;
    }

    protected void TrackResource(IAsyncDisposable resource) => _resources.Add(resource);

    protected TasksDatabaseTestFixture CreateDatabase(string prefix)
    {
        var fixture = new TasksDatabaseTestFixture(prefix);
        TrackResource(fixture);
        return fixture;
    }

    protected async System.Threading.Tasks.Task<TaskService> CreateTaskServiceAsync(string prefix)
    {
        var fixture = await CreateTaskServiceFixtureAsync(prefix);
        return fixture.Service;
    }

    protected async System.Threading.Tasks.Task<TaskServiceTestFixture> CreateTaskServiceFixtureAsync(string prefix)
    {
        var fixture = await TaskServiceTestFixture.CreateAsync(prefix);
        TrackResource(fixture);
        return fixture;
    }

    public System.Threading.Tasks.Task InitializeAsync() => System.Threading.Tasks.Task.CompletedTask;

    public async System.Threading.Tasks.Task DisposeAsync()
    {
        List<Exception>? failures = null;
        for (var index = _resources.Count - 1; index >= 0; index--)
        {
            try
            {
                await AsyncTest.CompletesWithinAsync(
                    _resources[index].DisposeAsync().AsTask(),
                    _lifecycleTimeout,
                    "Tasks test resource disposal did not complete");
            }
            catch (Exception exception)
            {
                (failures ??= []).Add(exception);
            }
        }
        _resources.Clear();

        if (failures is not null)
            throw new AggregateException(failures);
    }
}

public sealed class TasksDatabaseTestFixture : IAsyncDisposable
{
    private static readonly System.TimeSpan LifecycleTimeout = System.TimeSpan.FromSeconds(5);
    private readonly string _directory;

    public TasksDatabaseTestFixture(string prefix)
    {
        _directory = TestDirectory.Create(prefix);
        Factory = new SqliteConnectionFactory(System.IO.Path.Combine(_directory, "tasks.db"));
        Store = new TasksStore(Factory, NullLogger.Instance);
    }

    public SqliteConnectionFactory Factory { get; }
    public TasksStore Store { get; }
    public TasksWriteChannel? Channel { get; private set; }

    public async System.Threading.Tasks.Task<TasksWriteChannel> StartChannelAsync()
    {
        if (Channel is not null)
            return Channel;

        Channel = new TasksWriteChannel(Factory);
        await AsyncTest.CompletesWithinAsync(
            Channel.StartAsync(),
            LifecycleTimeout,
            "Tasks write channel startup did not complete");
        return Channel;
    }

    public async ValueTask DisposeAsync()
    {
        List<Exception>? failures = null;
        if (Channel is not null)
        {
            try
            {
                await AsyncTest.CompletesWithinAsync(
                    Channel.DisposeAsync().AsTask(),
                    LifecycleTimeout,
                    "Tasks write channel shutdown did not complete");
            }
            catch (Exception exception)
            {
                (failures ??= []).Add(exception);
            }
        }

        try
        {
            TestDirectory.Delete(_directory);
        }
        catch (Exception exception)
        {
            (failures ??= []).Add(exception);
        }

        if (failures is not null)
            throw new AggregateException(failures);
    }
}

public sealed class TaskServiceTestFixture : IAsyncDisposable
{
    private static readonly System.TimeSpan LifecycleTimeout = System.TimeSpan.FromSeconds(5);
    private readonly string _directory;

    private TaskServiceTestFixture(string directory, TaskService service)
    {
        _directory = directory;
        Service = service;
    }

    public TaskService Service { get; private set; }

    public static async System.Threading.Tasks.Task<TaskServiceTestFixture> CreateAsync(string prefix)
    {
        var directory = TestDirectory.Create(prefix);
        TaskService? service = null;
        try
        {
            service = new TaskService(directory, NullLogger.Instance);
            await AsyncTest.CompletesWithinAsync(
                service.StartAsync(),
                LifecycleTimeout,
                "Task service startup did not complete");
            return new TaskServiceTestFixture(directory, service);
        }
        catch (Exception primary)
        {
            List<Exception> failures = [primary];
            if (service is not null)
            {
                try
                {
                    await AsyncTest.CompletesWithinAsync(
                        service.DisposeAsync().AsTask(),
                        LifecycleTimeout,
                        "Task service cleanup after failed startup did not complete");
                }
                catch (Exception exception)
                {
                    failures.Add(exception);
                }
            }

            try
            {
                TestDirectory.Delete(directory);
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }

            if (failures.Count == 1)
                throw;
            throw new AggregateException(failures);
        }
    }

    public async System.Threading.Tasks.Task<TaskService> RestartAsync()
    {
        await AsyncTest.CompletesWithinAsync(
            Service.DisposeAsync().AsTask(),
            LifecycleTimeout,
            "Task service shutdown before restart did not complete");

        var replacement = new TaskService(_directory, NullLogger.Instance);
        Service = replacement;
        try
        {
            await AsyncTest.CompletesWithinAsync(
                replacement.StartAsync(),
                LifecycleTimeout,
                "Task service restart did not complete");
            return replacement;
        }
        catch (Exception primary)
        {
            try
            {
                await AsyncTest.CompletesWithinAsync(
                    replacement.DisposeAsync().AsTask(),
                    LifecycleTimeout,
                    "Task service cleanup after failed restart did not complete");
            }
            catch (Exception cleanup)
            {
                throw new AggregateException(primary, cleanup);
            }

            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        List<Exception>? failures = null;
        try
        {
            await AsyncTest.CompletesWithinAsync(
                Service.DisposeAsync().AsTask(),
                LifecycleTimeout,
                "Task service shutdown did not complete");
        }
        catch (Exception exception)
        {
            (failures ??= []).Add(exception);
        }

        try
        {
            TestDirectory.Delete(_directory);
        }
        catch (Exception exception)
        {
            (failures ??= []).Add(exception);
        }

        if (failures is not null)
            throw new AggregateException(failures);
    }
}

public sealed class FakeLaunchProfileResolver : ILaunchProfileResolver
{
    public LaunchProfileResolution? Result { get; set; } = new("claude", "default", "claude --resume", new Dictionary<string, string>());
    public LaunchProfileResolution? ResolveTaskProfile(string ws, string cardId) => Result;
}

public sealed class FakeWorktreeService : IWorktreeService
{
    public bool ShouldFail { get; set; }
    public int CreateCalls { get; private set; }
    public int RemoveCalls { get; private set; }
    public WorktreeCreationResult? CreateAsync(string ws, string branchSource, string branch, string? mergeTarget)
    {
        CreateCalls++;
        return ShouldFail ? null : new WorktreeCreationResult(branch, "/tmp/fake-worktree");
    }
    public bool RemoveAsync(string ws, string branchName) { RemoveCalls++; return true; }
}

public sealed class FakeNookHost : INookHost
{
    public bool ShouldFailCreate { get; set; }
    public bool ShouldFailEnv { get; set; }
    public int CreateCalls { get; private set; }
    public Dictionary<string, Dictionary<string, string>> InjectedEnvs { get; } = new();
    public Dictionary<string, string> BoundCards { get; } = new();
    public NookCreationResult? CreateNook(string? adapter, int cols, int rows)
    {
        CreateCalls++;
        return ShouldFailCreate ? null : new NookCreationResult($"nook-{CreateCalls}");
    }
    public bool InjectEnv(string nookId, IReadOnlyDictionary<string, string> env)
    {
        if (ShouldFailEnv) return false;
        InjectedEnvs[nookId] = new Dictionary<string, string>(env);
        return true;
    }
    public bool BindTaskCard(string nookId, string cardId) { BoundCards[nookId] = cardId; return true; }
    public bool RemoveNook(string nookId)
    {
        BoundCards.Remove(nookId);
        InjectedEnvs.Remove(nookId);
        return true;
    }
}

public sealed class FakeShoreService : IShoreService
{
    public List<string> CreatedShoreNames { get; } = new();
    public ShoreCreationResult? CreateShore(string ws, string name, string? parent)
    {
        CreatedShoreNames.Add(name);
        return new ShoreCreationResult($"shore-{CreatedShoreNames.Count}");
    }
    public bool RemoveShore(string bayId, string shoreId) => true;
}

public sealed class FakeAgentLauncher : IAgentLauncher
{
    public bool ShouldFail { get; set; }
    public string? Error { get; set; }
    public int LaunchCalls { get; private set; }
    public string? LastPrompt { get; private set; }
    public AdapterLaunchResult Launch(string nookId, string adapter, string cmd, IReadOnlyDictionary<string, string> env, string prompt)
    {
        LaunchCalls++;
        LastPrompt = prompt;
        return ShouldFail ? new AdapterLaunchResult("", false, Error ?? "launch failed") : new AdapterLaunchResult($"session-{LaunchCalls}", true, null);
    }
    public bool Stop(string adapterSessionId) => true;
}

public sealed class FakeAdapterResumeLauncher : IAdapterResumeLauncher
{
    public bool ShouldFail { get; set; }
    public string? Error { get; set; }
    public int ResumeCalls { get; private set; }
    public string? LastPriorSessionId { get; private set; }
    public string? LastAdapter { get; set; }
    public AdapterResumeResult Resume(string nookId, string adapter, string resolvedCommand, string priorAdapterSessionId, IReadOnlyDictionary<string, string> env)
    {
        ResumeCalls++;
        LastPriorSessionId = priorAdapterSessionId;
        return ShouldFail ? new AdapterResumeResult("", false, Error ?? "resume failed") : new AdapterResumeResult($"resumed-session-{ResumeCalls}", true, null);
    }
}

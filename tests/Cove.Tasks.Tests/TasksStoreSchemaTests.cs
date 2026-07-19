using System.Collections.Concurrent;
using Cove.Persistence;
using Cove.Tasks.Store;
using Cove.Testing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Tasks.Tests;

public sealed class TasksStoreSchemaTests : TasksTestBase
{
    private static readonly System.TimeSpan HandshakeTimeout = System.TimeSpan.FromSeconds(5);

    private TasksStore NewStore()
    {
        return CreateDatabase("cove-tasks-").Store;
    }

    [Fact]
    public void Schema_CreatesClean_AllTenTablesPresent()
    {
        var store = NewStore();
        var tables = store.ListTableNames();
        var expected = new[]
        {
            "migrations", "statuses", "cards", "comments", "labels",
            "card_labels", "task_counter", "task_runs", "task_run_segments", "card_schedules"
        };
        foreach (var name in expected)
            Assert.Contains(name, tables);
    }

    [Fact]
    public void Migration_ForwardRun_SetsUserVersionToOne()
    {
        var store = NewStore();
        Assert.Equal(TasksSchema.CurrentVersion, store.GetUserVersion());
    }

    [Fact]
    public void Migration_Idempotent_SecondBootstrapIsNoOp()
    {
        var store = NewStore();
        store.EnsureSchema();
        store.EnsureSchema();
        Assert.Equal(TasksSchema.CurrentVersion, store.GetUserVersion());
    }

    [Fact]
    public void SelfHeal_AddsMissingColumn_WithLoggedWarning()
    {
        var fixture = CreateDatabase("cove-tasks-");
        var factory = fixture.Factory;
        var store = fixture.Store;
        store.EnsureSchema();

        SchemaCorruptor.DropColumn(factory, "cards", "due_at");

        var warnings = new System.Collections.Generic.List<string>();
        var logger = new CapturingLogger(warnings);
        var healedStore = new TasksStore(factory, logger);
        healedStore.EnsureSchema();

        var columns = store.ListColumnNames("cards");
        Assert.Contains("due_at", columns);
        Assert.Contains(warnings, w => w.Contains("due_at") && w.Contains("cards"));
    }

    [Fact]
    public void SelfHeal_DoesNotAlterColumnType_OnNonAdditiveChange()
    {
        var fixture = CreateDatabase("cove-tasks-");
        var factory = fixture.Factory;
        var store = fixture.Store;
        store.EnsureSchema();

        SchemaCorruptor.ChangeColumnType(factory, "cards", "priority", "TEXT");

        var warnings = new System.Collections.Generic.List<string>();
        var logger = new CapturingLogger(warnings);
        var healedStore = new TasksStore(factory, logger);
        healedStore.EnsureSchema();

        var declaredType = store.GetColumnDeclaredType("cards", "priority");
        Assert.Equal("TEXT", declaredType);
        Assert.Contains(warnings, w => w.Contains("priority") && w.Contains("non-additive"));
    }

    [Fact]
    public async System.Threading.Tasks.Task ConcurrentWriters_ZeroSqliteBusy_ThroughChannelActor()
    {
        var fixture = CreateDatabase("cove-tasks-");
        var factory = fixture.Factory;
        var store = fixture.Store;
        store.EnsureSchema();

        var channel = await AsyncTest.CompletesWithinAsync(
            fixture.StartChannelAsync(),
            HandshakeTimeout,
            "tasks write channel did not start");
        var counter = new TaskCounterRepository(factory, channel);
        var tasks = new System.Threading.Tasks.Task[16];
        for (int i = 0; i < tasks.Length; i++)
            tasks[i] = counter.NextNumberAsync("ws1");
        await AsyncTest.CompletesWithinAsync(
            System.Threading.Tasks.Task.WhenAll(tasks),
            HandshakeTimeout,
            "concurrent counter writes did not complete");

        Assert.Equal(tasks.Length + 1, counter.PeekNumber("ws1"));
    }

    [Fact]
    public async System.Threading.Tasks.Task ConcurrentWriters_NumberingIsGapTolerant_AndOrdered()
    {
        var fixture = CreateDatabase("cove-tasks-");
        var factory = fixture.Factory;
        var store = fixture.Store;
        store.EnsureSchema();

        var channel = await AsyncTest.CompletesWithinAsync(
            fixture.StartChannelAsync(),
            HandshakeTimeout,
            "tasks write channel did not start");
        var counter = new TaskCounterRepository(factory, channel);
        var numbers = new ConcurrentBag<int>();
        var tasks = Enumerable.Range(0, 50).Select(_ => counter.NextNumberAsync("ws1")).ToArray();
        var results = await AsyncTest.CompletesWithinAsync(
            System.Threading.Tasks.Task.WhenAll(tasks),
            HandshakeTimeout,
            "ordered concurrent counter writes did not complete");
        foreach (var n in results) numbers.Add(n);

        var sorted = numbers.OrderBy(n => n).ToList();
        Assert.Equal(50, sorted.Count);
        Assert.Equal(Enumerable.Range(1, 50).ToList(), sorted);
    }

    [Fact]
    public async System.Threading.Tasks.Task WriteChannel_ExecutesOneAtATime_InSubmissionOrder()
    {
        var fixture = CreateDatabase("cove-tasks-");
        fixture.Store.EnsureSchema();
        var channel = await AsyncTest.CompletesWithinAsync(
            fixture.StartChannelAsync(),
            HandshakeTimeout,
            "tasks write channel did not start");
        var firstStarted = new System.Threading.Tasks.TaskCompletionSource<object?>(
            System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new System.Threading.Tasks.TaskCompletionSource<object?>(
            System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new List<int>();
        var completed = new List<int>();
        var active = 0;
        var maxActive = 0;

        var tasks = Enumerable.Range(0, 20).Select(index => channel.ExecuteAsync(async _ =>
        {
            var currentActive = Interlocked.Increment(ref active);
            UpdateMax(ref maxActive, currentActive);
            lock (started)
                started.Add(index);
            try
            {
                if (index == 0)
                {
                    firstStarted.TrySetResult(null);
                    await AsyncTest.CompletesWithinAsync(
                        releaseFirst.Task,
                        HandshakeTimeout,
                        "first queued write was not released");
                }
            }
            finally
            {
                Interlocked.Decrement(ref active);
            }
            lock (completed)
                completed.Add(index);
        })).ToArray();

        await AsyncTest.CompletesWithinAsync(
            firstStarted.Task,
            HandshakeTimeout,
            "first queued write did not start");
        Assert.Equal([0], started);
        releaseFirst.TrySetResult(null);
        await AsyncTest.CompletesWithinAsync(
            System.Threading.Tasks.Task.WhenAll(tasks),
            HandshakeTimeout,
            "queued writes did not complete");

        Assert.Equal(1, maxActive);
        Assert.Equal(Enumerable.Range(0, 20), started);
        Assert.Equal(Enumerable.Range(0, 20), completed);
    }

    private static void UpdateMax(ref int target, int value)
    {
        var observed = Volatile.Read(ref target);
        while (observed < value)
        {
            var prior = Interlocked.CompareExchange(ref target, value, observed);
            if (prior == observed)
                return;
            observed = prior;
        }
    }
}

internal sealed class CapturingLogger : ILogger
{
    private readonly System.Collections.Generic.List<string> _warnings;
    public CapturingLogger(System.Collections.Generic.List<string> warnings) => _warnings = warnings;
    public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullLogger.Instance.BeginScope(state);
    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (logLevel >= LogLevel.Warning)
            _warnings.Add(formatter(state, exception));
    }
}

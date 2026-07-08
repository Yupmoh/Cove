using System.Collections.Concurrent;
using Cove.Persistence;
using Cove.Tasks.Store;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Tasks.Tests;

public sealed class TasksStoreSchemaTests
{
    private static string NewDb() => System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cove-tasks-" + System.Guid.NewGuid().ToString("N") + ".db");

    private static TasksStore NewStore(string? path = null)
    {
        path ??= NewDb();
        var factory = new SqliteConnectionFactory(path);
        return new TasksStore(factory, NullLogger.Instance);
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
        var path = NewDb();
        var factory = new SqliteConnectionFactory(path);
        var store = new TasksStore(factory, NullLogger.Instance);
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
        var path = NewDb();
        var factory = new SqliteConnectionFactory(path);
        var store = new TasksStore(factory, NullLogger.Instance);
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
        var path = NewDb();
        var factory = new SqliteConnectionFactory(path);
        var store = new TasksStore(factory, NullLogger.Instance);
        store.EnsureSchema();

        await using var channel = new TasksWriteChannel(factory);
        await channel.StartAsync();
        var counter = new TaskCounterRepository(factory, channel);
        var errors = new ConcurrentBag<Exception>();
        var tasks = new System.Threading.Tasks.Task[16];
        for (int i = 0; i < tasks.Length; i++)
        {
            tasks[i] = System.Threading.Tasks.Task.Run(async () =>
            {
                try { await counter.NextNumberAsync("ws1"); }
                catch (Exception ex) { errors.Add(ex); }
            });
        }
        await System.Threading.Tasks.Task.WhenAll(tasks);

        Assert.Empty(errors);
        Assert.Equal(tasks.Length + 1, counter.PeekNumber("ws1"));
    }

    [Fact]
    public async System.Threading.Tasks.Task ConcurrentWriters_NumberingIsGapTolerant_AndOrdered()
    {
        var path = NewDb();
        var factory = new SqliteConnectionFactory(path);
        var store = new TasksStore(factory, NullLogger.Instance);
        store.EnsureSchema();

        await using var channel = new TasksWriteChannel(factory);
        await channel.StartAsync();
        var counter = new TaskCounterRepository(factory, channel);
        var numbers = new ConcurrentBag<int>();
        var tasks = Enumerable.Range(0, 50).Select(_ => counter.NextNumberAsync("ws1")).ToArray();
        var results = await System.Threading.Tasks.Task.WhenAll(tasks);
        foreach (var n in results) numbers.Add(n);

        var sorted = numbers.OrderBy(n => n).ToList();
        Assert.Equal(50, sorted.Count);
        Assert.Equal(Enumerable.Range(1, 50).ToList(), sorted);
    }

    [Fact]
    public async System.Threading.Tasks.Task WriteChannel_SerializesWrites_NoInterleaving()
    {
        var path = NewDb();
        var factory = new SqliteConnectionFactory(path);
        var store = new TasksStore(factory, NullLogger.Instance);
        store.EnsureSchema();

        await using var channel = new TasksWriteChannel(factory);
        await channel.StartAsync();
        var counter = new TaskCounterRepository(factory, channel);

        var observed = new ConcurrentBag<(int number, int threadId)>();
        var tasks = Enumerable.Range(0, 20).Select(_ => System.Threading.Tasks.Task.Run(async () =>
        {
            var n = await counter.NextNumberAsync("ws1");
            observed.Add((n, System.Environment.CurrentManagedThreadId));
        })).ToArray();
        await System.Threading.Tasks.Task.WhenAll(tasks);

        var byNumber = observed.OrderBy(x => x.number).ToList();
        var distinctThreadsPerNumber = byNumber.GroupBy(x => x.number).All(g => g.Count() == 1);
        Assert.True(distinctThreadsPerNumber);
        Assert.Equal(Enumerable.Range(1, 20).ToList(), byNumber.Select(x => x.number).ToList());
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

using System.Threading.Channels;
using Cove.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Cove.Tasks.Store;

internal sealed record WriteItem(
    System.Threading.Tasks.TaskCompletionSource<object?> Completion,
    Func<SqliteConnection, System.Threading.Tasks.Task> Run);

public sealed class TasksWriteChannel : IAsyncDisposable
{
    private readonly SqliteConnectionFactory _factory;
    private readonly ILogger _logger;
    private readonly Channel<WriteItem> _queue;
    private System.Threading.Tasks.Task? _loop;
    private volatile bool _completed;

    public TasksWriteChannel(SqliteConnectionFactory factory, ILogger? logger = null)
    {
        _factory = factory;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
        _queue = Channel.CreateUnbounded<WriteItem>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });
    }

    public System.Threading.Tasks.Task StartAsync()
    {
        _loop = System.Threading.Tasks.Task.Run(RunLoopAsync);
        return System.Threading.Tasks.Task.CompletedTask;
    }

    private async System.Threading.Tasks.Task RunLoopAsync()
    {
        await foreach (var item in _queue.Reader.ReadAllAsync())
        {
            try
            {
                using var conn = _factory.Open();
                await item.Run(conn);
                item.Completion.TrySetResult(null);
            }
            catch (Exception ex)
            {
                _logger.WriteChannelWorkFailed(ex.Message);
                item.Completion.TrySetException(ex);
            }
        }
        FaultPending();
    }

    private void FaultPending()
    {
        _completed = true;
        while (_queue.Reader.TryRead(out var pending))
            pending.Completion.TrySetCanceled();
    }

    public async System.Threading.Tasks.Task<T> ExecuteAsync<T>(Func<SqliteConnection, System.Threading.Tasks.Task<T>> work)
    {
        if (_completed)
            throw new ObjectDisposedException(nameof(TasksWriteChannel));
        var tcs = new System.Threading.Tasks.TaskCompletionSource<object?>(System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously);
        await _queue.Writer.WriteAsync(new WriteItem(tcs, async conn =>
        {
            var result = await work(conn);
            tcs.TrySetResult(result);
        }));
        return (T)(await tcs.Task)!;
    }

    public async System.Threading.Tasks.Task ExecuteAsync(Func<SqliteConnection, System.Threading.Tasks.Task> work)
    {
        if (_completed)
            throw new ObjectDisposedException(nameof(TasksWriteChannel));
        var tcs = new System.Threading.Tasks.TaskCompletionSource<object?>(System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously);
        await _queue.Writer.WriteAsync(new WriteItem(tcs, work));
        await tcs.Task;
    }

    public async ValueTask DisposeAsync()
    {
        _queue.Writer.TryComplete();
        if (_loop is not null)
            await _loop.ConfigureAwait(false);
        FaultPending();
    }
}

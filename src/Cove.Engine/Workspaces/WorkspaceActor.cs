using System.Threading.Channels;

namespace Cove.Engine.Workspaces;

public sealed class WorkspaceActor : IAsyncDisposable
{
    private readonly Channel<(Func<WorkspaceModel, WorkspaceModel> Mutation, TaskCompletionSource Done)> _channel;
    private readonly Task _pump;
    private readonly Action<WorkspaceModel>? _onChanged;
    private volatile WorkspaceModel _state;

    public WorkspaceActor(WorkspaceModel initial, Action<WorkspaceModel>? onChanged = null)
    {
        _state = initial;
        _onChanged = onChanged;
        _channel = Channel.CreateUnbounded<(Func<WorkspaceModel, WorkspaceModel>, TaskCompletionSource)>(
            new UnboundedChannelOptions { SingleReader = true });
        _pump = Task.Run(PumpAsync);
    }

    public WorkspaceModel State => _state;

    public Task Mutate(Func<WorkspaceModel, WorkspaceModel> mutation)
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_channel.Writer.TryWrite((mutation, done)))
            done.SetException(new InvalidOperationException("workspace actor is closed"));
        return done.Task;
    }

    private async Task PumpAsync()
    {
        await foreach (var (mutation, done) in _channel.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            try
            {
                _state = mutation(_state);
                _onChanged?.Invoke(_state);
                done.SetResult();
            }
            catch (Exception ex)
            {
                done.SetException(ex);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _channel.Writer.TryComplete();
        await _pump.ConfigureAwait(false);
    }
}

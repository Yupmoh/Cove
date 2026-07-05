using System.Threading.Channels;

namespace Cove.Engine.Workspaces;

public sealed class Actor<T> : IAsyncDisposable where T : class
{
    private readonly Channel<(Func<T, T> Mutation, TaskCompletionSource Done)> _channel;
    private readonly Task _pump;
    private readonly Action<T>? _onChanged;
    private volatile T _state;

    public Actor(T initial, Action<T>? onChanged = null)
    {
        _state = initial;
        _onChanged = onChanged;
        _channel = Channel.CreateUnbounded<(Func<T, T>, TaskCompletionSource)>(
            new UnboundedChannelOptions { SingleReader = true });
        _pump = Task.Run(PumpAsync);
    }

    public T State => _state;

    public Task Mutate(Func<T, T> mutation)
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_channel.Writer.TryWrite((mutation, done)))
            done.SetException(new InvalidOperationException("actor is closed"));
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

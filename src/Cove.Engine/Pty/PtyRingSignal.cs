using System.Threading;
using System.Threading.Tasks;

namespace Cove.Engine.Pty;

public sealed class PtyRingSignal
{
    private volatile TaskCompletionSource _tcs =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task WaitAsync() => _tcs.Task;

    public void Set()
    {
        var prev = Interlocked.Exchange(ref _tcs,
            new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));
        prev.TrySetResult();
    }
}

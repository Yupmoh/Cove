using System.Diagnostics;

namespace Cove.Testing;

public static class AsyncTest
{
    public static async Task EventuallyAsync(
        Func<bool> condition,
        TimeSpan timeout,
        string failureMessage,
        CancellationToken cancellationToken = default,
        TimeSpan? pollInterval = null)
    {
        var interval = pollInterval ?? TimeSpan.FromMilliseconds(10);
        var stopwatch = Stopwatch.StartNew();
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (condition())
                return;
            if (stopwatch.Elapsed >= timeout)
                throw new TimeoutException(failureMessage);
            var remaining = timeout - stopwatch.Elapsed;
            await Task.Delay(remaining < interval ? remaining : interval, cancellationToken).ConfigureAwait(false);
        }
    }

    public static async Task EventuallyAsync(
        Func<Task<bool>> condition,
        TimeSpan timeout,
        string failureMessage,
        CancellationToken cancellationToken = default,
        TimeSpan? pollInterval = null)
    {
        var interval = pollInterval ?? TimeSpan.FromMilliseconds(10);
        var stopwatch = Stopwatch.StartNew();
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await condition().ConfigureAwait(false))
                return;
            if (stopwatch.Elapsed >= timeout)
                throw new TimeoutException(failureMessage);
            var remaining = timeout - stopwatch.Elapsed;
            await Task.Delay(remaining < interval ? remaining : interval, cancellationToken).ConfigureAwait(false);
        }
    }

    public static async Task CompletesWithinAsync(
        Task task,
        TimeSpan timeout,
        string failureMessage,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await task.WaitAsync(timeout, cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException exception)
        {
            throw new TimeoutException(failureMessage, exception);
        }
    }

    public static async Task<T> CompletesWithinAsync<T>(
        Task<T> task,
        TimeSpan timeout,
        string failureMessage,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await task.WaitAsync(timeout, cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException exception)
        {
            throw new TimeoutException(failureMessage, exception);
        }
    }
}

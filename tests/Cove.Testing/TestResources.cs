using System.Diagnostics;

namespace Cove.Testing;

public static class TestDirectory
{
    public static string Create(string prefix, string? parent = null)
    {
        var path = Path.Combine(parent ?? Path.GetTempPath(), prefix + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    public static void Delete(string path)
    {
        if (!Directory.Exists(path))
            return;
        Directory.Delete(path, recursive: true);
        if (Directory.Exists(path))
            throw new IOException($"test directory remains after cleanup: {path}");
    }
}

public static class TestFile
{
    public static void Delete(string path)
    {
        if (!File.Exists(path))
            return;
        File.Delete(path);
        if (File.Exists(path))
            throw new IOException($"test file remains after cleanup: {path}");
    }
}

public static class TestProcess
{
    private static readonly TimeSpan CleanupTimeout = TimeSpan.FromSeconds(5);

    public static async Task<int> WaitForExitAsync(
        Process process,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await process.WaitForExitAsync(cancellationToken).WaitAsync(timeout, cancellationToken).ConfigureAwait(false);
            return process.ExitCode;
        }
        catch (Exception failure) when (failure is TimeoutException or OperationCanceledException)
        {
            try
            {
                if (!process.HasExited)
                {
                    try
                    {
                        process.Kill(entireProcessTree: true);
                    }
                    catch (InvalidOperationException) when (process.HasExited)
                    {
                    }
                }
                await process.WaitForExitAsync(CancellationToken.None)
                    .WaitAsync(CleanupTimeout, CancellationToken.None)
                    .ConfigureAwait(false);
            }
            catch (Exception cleanupFailure)
            {
                throw new AggregateException(
                    failure,
                    new InvalidOperationException(
                        $"Process {process.Id} did not terminate within {CleanupTimeout} during bounded failure cleanup.",
                        cleanupFailure));
            }
            throw;
        }
    }
}

public sealed class ProcessEnvironmentScope : IAsyncDisposable
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private readonly Dictionary<string, string?> _previous;
    private bool _disposed;

    private ProcessEnvironmentScope(Dictionary<string, string?> previous)
    {
        _previous = previous;
    }

    public static async ValueTask<ProcessEnvironmentScope> SetAsync(
        IReadOnlyDictionary<string, string?> variables,
        CancellationToken cancellationToken = default)
    {
        await Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        var previous = new Dictionary<string, string?>(variables.Count, StringComparer.Ordinal);
        try
        {
            foreach (var variable in variables)
            {
                previous[variable.Key] = Environment.GetEnvironmentVariable(variable.Key);
                Environment.SetEnvironmentVariable(variable.Key, variable.Value);
            }
            return new ProcessEnvironmentScope(previous);
        }
        catch
        {
            try
            {
                foreach (var variable in previous)
                    Environment.SetEnvironmentVariable(variable.Key, variable.Value);
            }
            finally
            {
                Gate.Release();
            }
            throw;
        }
    }

    public static ValueTask<ProcessEnvironmentScope> SetAsync(
        string variable,
        string? value,
        CancellationToken cancellationToken = default) =>
        SetAsync(new Dictionary<string, string?> { [variable] = value }, cancellationToken);

    public ValueTask DisposeAsync()
    {
        if (_disposed)
            return ValueTask.CompletedTask;
        try
        {
            foreach (var variable in _previous)
                Environment.SetEnvironmentVariable(variable.Key, variable.Value);
        }
        finally
        {
            _disposed = true;
            Gate.Release();
        }
        return ValueTask.CompletedTask;
    }
}

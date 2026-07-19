using System.Diagnostics;
using System.Text;

namespace Cove.Platform;

public sealed record ProcessRunRequest(
    string FileName,
    string WorkingDirectory,
    IReadOnlyList<string> Arguments,
    IReadOnlyDictionary<string, string>? Environment,
    TimeSpan Timeout);

public sealed record ProcessRunResult(
    bool Started,
    bool TimedOut,
    int ExitCode,
    string Stdout,
    string Stderr,
    long ElapsedMilliseconds);

public interface IProcessRunner
{
    Task<ProcessRunResult> RunAsync(ProcessRunRequest request, CancellationToken cancellationToken = default);
}

public sealed class SystemProcessRunner : IProcessRunner
{
    public async Task<ProcessRunResult> RunAsync(ProcessRunRequest request, CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo(request.FileName)
        {
            WorkingDirectory = request.WorkingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var argument in request.Arguments)
            startInfo.ArgumentList.Add(argument);
        if (request.Environment is not null)
            foreach (var (key, value) in request.Environment)
                startInfo.Environment[key] = value;

        using var process = new Process { StartInfo = startInfo };
        var stopwatch = Stopwatch.StartNew();
        if (!process.Start())
            return new ProcessRunResult(false, false, -1, "", "", stopwatch.ElapsedMilliseconds);

        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(request.Timeout);
        var timedOut = false;
        try
        {
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            timedOut = true;
            TryKill(process);
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
            TryKill(process);
            throw;
        }

        stopwatch.Stop();
        var capturedOut = await stdout.ConfigureAwait(false);
        var capturedError = await stderr.ConfigureAwait(false);
        return new ProcessRunResult(
            true,
            timedOut,
            timedOut ? -1 : process.ExitCode,
            capturedOut,
            capturedError,
            stopwatch.ElapsedMilliseconds);
    }

    private static void TryKill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
        }
    }
}

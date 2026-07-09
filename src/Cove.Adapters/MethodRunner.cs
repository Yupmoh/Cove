using System.Diagnostics;
using System.Text.Json;

namespace Cove.Adapters;

public sealed record MethodResult(int ExitCode, string Stdout, string Stderr, JsonElement? Json)
{
    public bool Ok => ExitCode == 0;
    public bool GracefulFailure => ExitCode == 1;
    public bool Error => ExitCode >= 2;
}

public sealed class MethodRunner
{
    private readonly Func<string?> _bashResolver;

    public MethodRunner(Func<string?>? bashResolver = null)
    {
        _bashResolver = bashResolver ?? DefaultBashResolver;
    }

    private static string? DefaultBashResolver() => BashLocator.Find();

    public async Task<MethodResult> RunAsync(
        string adapterDir,
        string script,
        IReadOnlyList<string> args,
        TimeSpan timeout,
        IReadOnlyDictionary<string, string>? env = null,
        CancellationToken cancellationToken = default)
    {
        var bashExe = _bashResolver();
        if (bashExe is null)
            return new MethodResult(-1, "", "no bash available", null);

        var scriptPath = Path.Combine(adapterDir, script);
        var psi = new ProcessStartInfo
        {
            FileName = bashExe,
            WorkingDirectory = adapterDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8,
        };
        psi.ArgumentList.Add(scriptPath);
        foreach (var arg in args)
            psi.ArgumentList.Add(arg);

        psi.Environment["COVE_ADAPTER_DIR"] = adapterDir;
        psi.Environment["COVE_SDK_VERSION"] = "2";
        if (env is not null)
            foreach (var kv in env)
                psi.Environment[kv.Key] = kv.Value;

        using var process = new Process { StartInfo = psi };
        process.Start();

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            return new MethodResult(-1, "", "timeout: killed", null);
        }

        var stdout = await stdoutTask.ConfigureAwait(false);
        var stderr = await stderrTask.ConfigureAwait(false);

        JsonElement? json = null;
        if (process.ExitCode == 0)
        {
            try
            {
                json = JsonDocument.Parse(stdout).RootElement.Clone();
            }
            catch (JsonException) { }
        }

        return new MethodResult(process.ExitCode, stdout, stderr, json);
    }
}

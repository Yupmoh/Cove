using System.Text.Json;
using Cove.Platform;
using Microsoft.Extensions.Logging;

namespace Cove.Adapters;

public sealed record MethodResult(int ExitCode, string Stdout, string Stderr, JsonElement? Json)
{
    public bool Ok => ExitCode == 0;
    public bool GracefulFailure => ExitCode == 1;
    public bool Error => ExitCode >= 2;
}

public sealed class MethodRunner
{
    private readonly IBashLocator _bashLocator;
    private readonly IProcessRunner _processRunner;
    private readonly ILogger? _logger;

    public MethodRunner(
        IBashLocator? bashLocator = null,
        IProcessRunner? processRunner = null,
        ILogger? logger = null)
    {
        _bashLocator = bashLocator ?? new BashLocator();
        _processRunner = processRunner ?? new SystemProcessRunner();
        _logger = logger;
    }

    public async Task<MethodResult> RunAsync(
        string adapterDir,
        string script,
        IReadOnlyList<string> args,
        TimeSpan timeout,
        IReadOnlyDictionary<string, string>? env = null,
        CancellationToken cancellationToken = default)
    {
        var adapterName = Path.GetFileName(adapterDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var bash = _bashLocator.Find();
        if (bash is null)
        {
            _logger?.MethodNoBash(adapterName, script);
            return new MethodResult(-1, "", "no bash available", null);
        }

        var arguments = new string[args.Count + 1];
        arguments[0] = Path.Combine(adapterDir, script);
        for (var i = 0; i < args.Count; i++)
            arguments[i + 1] = args[i];

        var environment = env is null
            ? new Dictionary<string, string>(2, StringComparer.Ordinal)
            : new Dictionary<string, string>(env, StringComparer.Ordinal);
        environment["COVE_ADAPTER_DIR"] = adapterDir;
        environment["COVE_SDK_VERSION"] = "2";

        ProcessRunResult process;
        try
        {
            process = await _processRunner.RunAsync(
                new ProcessRunRequest(bash, adapterDir, arguments, environment, timeout),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger?.MethodStartFailed(adapterName, script, ex.Message);
            return new MethodResult(-1, "", ex.Message, null);
        }

        if (!process.Started)
        {
            _logger?.MethodStartFailed(adapterName, script, "process did not start");
            return new MethodResult(-1, "", "process did not start", null);
        }
        if (process.TimedOut)
        {
            _logger?.MethodTimedOut(adapterName, script, (long)timeout.TotalMilliseconds);
            return new MethodResult(-1, process.Stdout, "timeout: killed", null);
        }

        _logger?.MethodCompleted(adapterName, script, process.ExitCode, process.ElapsedMilliseconds);
        _logger?.MethodStdoutDigest(adapterName, script, process.Stdout.Length, Digest(process.Stdout));
        if (!string.IsNullOrWhiteSpace(process.Stderr))
            _logger?.MethodStderr(adapterName, script, process.Stderr.Trim());

        JsonElement? json = null;
        if (process.ExitCode == 0)
        {
            try
            {
                json = JsonDocument.Parse(process.Stdout).RootElement.Clone();
            }
            catch (JsonException ex)
            {
                _logger?.MethodStdoutNotJson(adapterName, script, ex.Message);
            }
        }
        return new MethodResult(process.ExitCode, process.Stdout, process.Stderr, json);
    }

    private static string Digest(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.Length > 200)
            trimmed = trimmed[..200];
        return trimmed.Replace('\n', ' ').Replace('\r', ' ');
    }
}

using Cove.Adapters;
using Microsoft.Extensions.Logging;

namespace Cove.Engine.Launch;

public interface ILaunchProcessAcquirer
{
    Task<MethodResult> RunMethodAsync(
        LaunchAdapter adapter,
        string methodName,
        string script,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default);

    Task<string?> AcquireBinaryAsync(
        LaunchAdapter adapter,
        CancellationToken cancellationToken = default);

    BinaryDiscoveryResult Describe(AdapterManifest manifest);

    void RefreshLoginShellPath();
}

public sealed class LaunchProcessAcquirer(
    MethodRunner methodRunner,
    BinaryDiscoveryService binaryDiscovery,
    string? loginShellPath = null,
    ILogger? logger = null) : ILaunchProcessAcquirer
{
    private string? _loginShellPath = loginShellPath;

    public async Task<MethodResult> RunMethodAsync(
        LaunchAdapter adapter,
        string methodName,
        string script,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default)
    {
        var startTimestamp =
            System.Diagnostics.Stopwatch.GetTimestamp();
        var result = await methodRunner.RunAsync(
            adapter.Directory,
            script,
            arguments,
            TimeSpan.FromSeconds(5),
            cancellationToken: cancellationToken).ConfigureAwait(false);
        logger?.LaunchMethodRunner(
            adapter.Manifest.Name,
            methodName,
            System.Diagnostics.Stopwatch
                .GetElapsedTime(startTimestamp)
                .TotalMilliseconds,
            result.ExitCode,
            result.Ok);
        return result;
    }

    public async Task<string?> AcquireBinaryAsync(
        LaunchAdapter adapter,
        CancellationToken cancellationToken = default)
    {
        var manifest = adapter.Manifest;
        if (manifest.BinaryDiscovery is { } discovery)
        {
            var discoveryResult = binaryDiscovery.Discover(
                discovery,
                _loginShellPath);
            logger?.LaunchBinaryDiscovery(
                manifest.Name,
                discoveryResult.State.ToString(),
                discoveryResult.BinaryPath ?? "");
            return
                discoveryResult.State == AdapterDetectionState.Detected
                && !string.IsNullOrEmpty(discoveryResult.BinaryPath)
                    ? discoveryResult.BinaryPath
                    : null;
        }

        if (!manifest.Methods.TryGetValue(
                "detect_binary",
                out var method)
            || method.Script is null)
        {
            return null;
        }

        var methodResult = await RunMethodAsync(
            adapter,
            "detect_binary",
            method.Script,
            Array.Empty<string>(),
            cancellationToken).ConfigureAwait(false);
        if (methodResult.Ok
            && methodResult.Json is { } json
            && json.TryGetProperty("path", out var pathProperty))
        {
            var path = pathProperty.GetString();
            return string.IsNullOrEmpty(path) ? null : path;
        }

        return null;
    }

    public BinaryDiscoveryResult Describe(AdapterManifest manifest)
    {
        if (manifest.BinaryDiscovery is not { } discovery)
        {
            logger?.AdapterBinaryDiscoveryUnavailable(manifest.Name);
            return new BinaryDiscoveryResult(
                AdapterDetectionState.Missing,
                null,
                null);
        }

        var result = binaryDiscovery.Discover(
            discovery,
            _loginShellPath);
        logger?.LaunchBinaryDiscovery(
            manifest.Name,
            result.State.ToString(),
            result.BinaryPath ?? "");
        return result;
    }

    public void RefreshLoginShellPath()
    {
        _loginShellPath = Cove.Platform.LoginShellPath.Probe();
    }
}

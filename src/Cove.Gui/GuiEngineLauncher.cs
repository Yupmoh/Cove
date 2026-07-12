using System.Diagnostics;
using Cove.Protocol;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cove.Gui;

public static class GuiEngineLauncher
{
    public static ILogger Logger { get; set; } = NullLogger.Instance;

    public static async Task<Stream> ConnectOrSpawnAsync(string channel, CancellationToken ct)
    {
        var target = GuiLogging.EndpointFor(channel);
        Logger.LauncherDialAttempt(channel, target);
        Stream? s = await TryDialAsync(channel, target, ct).ConfigureAwait(false);
        if (s is not null)
            return s;

        SpawnEngine(channel);

        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < ProtocolConstants.ReadinessTimeoutMs)
        {
            await Task.Delay(ProtocolConstants.SpawnPollMs, ct).ConfigureAwait(false);
            s = await TryDialAsync(channel, target, ct).ConfigureAwait(false);
            if (s is not null)
                return s;
        }
        Logger.LauncherSpawnTimeout(channel, target, sw.ElapsedMilliseconds);
        throw new InvalidOperationException($"cove engine did not become connectable on channel {channel}");
    }

    private static async Task<Stream?> TryDialAsync(string channel, string target, CancellationToken ct)
    {
        try
        {
            return await EndpointDialer.DialAsync(channel, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger.LauncherDialFailed(channel, target, ex.Message);
            return null;
        }
    }

    private static void SpawnEngine(string channel)
    {
        string exe = ResolveEnginePath();
        Logger.LauncherSpawn(channel, exe);
        var psi = new ProcessStartInfo
        {
            FileName = exe,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add("daemon");
        psi.ArgumentList.Add("run");
        psi.ArgumentList.Add("--channel");
        psi.ArgumentList.Add(channel);
        Process.Start(psi);
    }

    private static string ResolveEnginePath()
    {
        string? overridePath = Environment.GetEnvironmentVariable("COVE_ENGINE");
        if (!string.IsNullOrEmpty(overridePath) && File.Exists(overridePath))
            return overridePath;
        string exeName = OperatingSystem.IsWindows() ? "cove.exe" : "cove";
        string bundled = Path.Combine(AppContext.BaseDirectory, exeName);
        if (File.Exists(bundled))
            return bundled;
        throw new FileNotFoundException(
            $"cove engine not found; set COVE_ENGINE or place {exeName} next to the app");
    }
}

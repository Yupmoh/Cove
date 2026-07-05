using System.Diagnostics;
using Cove.Protocol;

namespace Cove.Gui;

public static class GuiEngineLauncher
{
    public static async Task<Stream> ConnectOrSpawnAsync(string channel, CancellationToken ct)
    {
        Stream? s = await TryDialAsync(channel, ct).ConfigureAwait(false);
        if (s is not null)
            return s;

        SpawnEngine(channel);

        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < ProtocolConstants.ReadinessTimeoutMs)
        {
            await Task.Delay(ProtocolConstants.SpawnPollMs, ct).ConfigureAwait(false);
            s = await TryDialAsync(channel, ct).ConfigureAwait(false);
            if (s is not null)
                return s;
        }
        throw new InvalidOperationException($"cove engine did not become connectable on channel {channel}");
    }

    private static async Task<Stream?> TryDialAsync(string channel, CancellationToken ct)
    {
        try
        {
            return await EndpointDialer.DialAsync(channel, ct).ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }

    private static void SpawnEngine(string channel)
    {
        string exe = ResolveEnginePath();
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

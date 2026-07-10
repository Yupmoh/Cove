using Cove.Engine.Daemon;
using Cove.Platform;
using Cove.Platform.Ipc;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class DaemonSingletonOrderTests
{
    [Fact]
    public async Task SecondInstance_ExitsWithoutTouchingHookPort()
    {
        var parent = Path.Combine(Path.GetTempPath(), "cove-singleton-" + Guid.NewGuid().ToString("N").Substring(0, 8));
        var prev = Environment.GetEnvironmentVariable("COVE_DATA_DIR");
        Environment.SetEnvironmentVariable("COVE_DATA_DIR", parent);
        try
        {
            var dd = CoveDataDir.Resolve(CoveChannel.Dev);
            var paths = new DaemonPaths(dd);
            CoveTree.Ensure(dd);
            var portFile = Path.Combine(dd.Root, "hook-port");
            await File.WriteAllTextAsync(portFile, "51525");
            using var holder = SingleInstanceGuard.TryAcquire(paths.PidFilePath);
            Assert.NotNull(holder);

            var endpoint = ControlEndpointFactory.FromSocketPath(dd.SocketPath);
            var host = new DaemonHost(paths, endpoint, exitWhenIdle: false);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            var exit = await host.RunAsync(cts.Token);

            Assert.Equal(0, exit);
            Assert.Equal("51525", await File.ReadAllTextAsync(portFile));
        }
        finally
        {
            Environment.SetEnvironmentVariable("COVE_DATA_DIR", prev);
            try { Directory.Delete(parent, true); } catch { }
        }
    }
}

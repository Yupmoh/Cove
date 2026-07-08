using System.IO;
using Cove.Cli;
using Xunit;

namespace Cove.Cli.Tests;

public sealed class MigrateVerbTests
{
    [Fact]
    public async Task Migrate_NoOpOnFreshDataDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "cove-migtest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        Directory.CreateDirectory(Path.Combine(dir, "adapters"));
        var prev = System.Environment.GetEnvironmentVariable("COVE_DATA_DIR");
        System.Environment.SetEnvironmentVariable("COVE_DATA_DIR", dir);
        try
        {
            var paths = new Cove.Engine.Daemon.DaemonPaths(Cove.Platform.CoveDataDir.Resolve(Cove.Platform.CoveChannel.Stable));
            var endpoint = Cove.Platform.Ipc.ControlEndpointFactory.FromSocketPath(paths.DataDir.SocketPath);
            var ctx = new CommandContext(paths, endpoint, new System.IO.StringWriter());
            var result = await CliCommands.Migrate(ctx);
            Assert.Equal(0, result);
        }
        finally
        {
            System.Environment.SetEnvironmentVariable("COVE_DATA_DIR", prev);
            try { Directory.Delete(dir, true); } catch { }
        }
    }
}

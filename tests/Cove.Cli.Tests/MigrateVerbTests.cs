using System.IO;
using Cove.Cli;
using Cove.Testing;
using Xunit;

namespace Cove.Cli.Tests;

[Collection(CliCollectionFixture.Name)]
public sealed class MigrateVerbTests
{
    [Fact]
    public async Task Migrate_NoOpOnFreshDataDir()
    {
        var dir = TestDirectory.Create("cove-migtest-");
        Directory.CreateDirectory(Path.Combine(dir, "adapters"));
        try
        {
            await using (await ProcessEnvironmentScope.SetAsync("COVE_DATA_DIR", dir))
            {
                var paths = new Cove.Engine.Daemon.DaemonPaths(Cove.Platform.CoveDataDir.Resolve(Cove.Platform.CoveChannel.Stable));
                var endpoint = Cove.Platform.Ipc.ControlEndpointFactory.FromSocketPath(paths.DataDir.SocketPath);
                var ctx = new CommandContext(paths, endpoint, new System.IO.StringWriter());
                var result = await CliCommands.Migrate(ctx);
                Assert.Equal(0, result);
            }
        }
        finally
        {
            TestDirectory.Delete(dir);
        }
    }
}

using System.IO;
using Cove.Cli;
using Cove.Platform;
using Xunit;

namespace Cove.Cli.Tests;

public sealed class DirectStoreFallbackTests
{
    [Fact]
    public async Task ConfigGet_WorksDaemonDown_DirectFileRead()
    {
        var dir = Path.Combine(Path.GetTempPath(), "cove-direct-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "config.json"), "{\"theme\":\"dracula\",\"terminal\":{\"fontSize\":16}}");
        var prev = System.Environment.GetEnvironmentVariable("COVE_DATA_DIR");
        System.Environment.SetEnvironmentVariable("COVE_DATA_DIR", dir);
        try
        {
            var paths = new Cove.Engine.Daemon.DaemonPaths(CoveDataDir.Resolve(CoveChannel.Stable));
            var endpoint = Cove.Platform.Ipc.ControlEndpointFactory.FromSocketPath(paths.DataDir.SocketPath);
            var stdout = new System.IO.StringWriter();
            var ctx = new CommandContext(paths, endpoint, stdout, args: new[] { "theme" });
            var result = await CliCommands.ConfigGet(ctx);
            Assert.Equal(0, result);
            Assert.Contains("dracula", stdout.ToString());
        }
        finally
        {
            System.Environment.SetEnvironmentVariable("COVE_DATA_DIR", prev);
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    [Fact]
    public async Task ConfigGet_NestedKey_WorksDaemonDown()
    {
        var dir = Path.Combine(Path.GetTempPath(), "cove-direct-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "config.json"), "{\"terminal\":{\"fontSize\":16}}");
        var prev = System.Environment.GetEnvironmentVariable("COVE_DATA_DIR");
        System.Environment.SetEnvironmentVariable("COVE_DATA_DIR", dir);
        try
        {
            var paths = new Cove.Engine.Daemon.DaemonPaths(CoveDataDir.Resolve(CoveChannel.Stable));
            var endpoint = Cove.Platform.Ipc.ControlEndpointFactory.FromSocketPath(paths.DataDir.SocketPath);
            var stdout = new System.IO.StringWriter();
            var ctx = new CommandContext(paths, endpoint, stdout, args: new[] { "terminal.fontSize" });
            var result = await CliCommands.ConfigGet(ctx);
            Assert.Equal(0, result);
            Assert.Contains("16", stdout.ToString());
        }
        finally
        {
            System.Environment.SetEnvironmentVariable("COVE_DATA_DIR", prev);
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    [Fact]
    public async Task ConfigGet_MissingKey_Returns1()
    {
        var dir = Path.Combine(Path.GetTempPath(), "cove-direct-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "config.json"), "{\"theme\":\"dracula\"}");
        var prev = System.Environment.GetEnvironmentVariable("COVE_DATA_DIR");
        System.Environment.SetEnvironmentVariable("COVE_DATA_DIR", dir);
        try
        {
            var paths = new Cove.Engine.Daemon.DaemonPaths(CoveDataDir.Resolve(CoveChannel.Stable));
            var endpoint = Cove.Platform.Ipc.ControlEndpointFactory.FromSocketPath(paths.DataDir.SocketPath);
            var stderr = new System.IO.StringWriter();
            var ctx = new CommandContext(paths, endpoint, new System.IO.StringWriter(), stderr, args: new[] { "nonexistent" });
            var result = await CliCommands.ConfigGet(ctx);
            Assert.Equal(1, result);
            Assert.Contains("not found", stderr.ToString());
        }
        finally
        {
            System.Environment.SetEnvironmentVariable("COVE_DATA_DIR", prev);
            try { Directory.Delete(dir, true); } catch { }
        }
    }
}

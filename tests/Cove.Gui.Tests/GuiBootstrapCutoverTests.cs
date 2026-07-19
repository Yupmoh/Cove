using Cove.Platform.Ipc;
using Cove.Testing;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Cove.Gui.Tests;

public sealed class GuiBootstrapCutoverTests
{
    [PlatformFact(TestOperatingSystem.Unix)]
    [Trait(TestTraits.Category, TestTraits.Platform)]
    public async Task Endpoint_dialer_connects_to_platform_socket_endpoint()
    {
        string root = Path.Combine("/tmp", "cove-gui-ipc-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(root);
        await using var environment = await ProcessEnvironmentScope.SetAsync("COVE_DATA_DIR", root);
        string socketPath = Path.Combine(root, "ipc", "dev.sock");
        Directory.CreateDirectory(Path.GetDirectoryName(socketPath)!);
        var endpoint = ControlEndpointFactory.FromSocketPath(socketPath);

        try
        {
            await using var listener = endpoint.Bind();
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            Task<Stream> accept = listener.AcceptAsync(cancellation.Token).AsTask();
            await using Stream client = await EndpointDialer.DialAsync("dev", cancellation.Token);
            await using Stream server = await accept;

            await client.WriteAsync("gui"u8.ToArray(), cancellation.Token);
            var buffer = new byte[3];
            await server.ReadExactlyAsync(buffer, cancellation.Token);

            Assert.Equal("gui"u8.ToArray(), buffer);
        }
        finally
        {
            TestFile.Delete(socketPath);
            TestDirectory.Delete(root);
        }
    }

    [Fact]
    public async Task Gui_logging_creates_gui_log_in_resolved_logs_directory()
    {
        using var directory = GuiTestDirectory.Create("cove-gui-log-");
        await using var environment = await ProcessEnvironmentScope.SetAsync(
            new Dictionary<string, string?>
            {
                ["COVE_DATA_DIR"] = directory.Path,
                ["COVE_CHANNEL"] = "dev",
                ["COVE_LOG_LEVEL"] = "information",
            });

        using (ILoggerFactory factory = GuiLogging.CreateFactory())
            factory.CreateLogger("test").LogInformation("gui bootstrap log probe");

        string path = Path.Combine(directory.Path, "logs", "gui.log");
        Assert.True(File.Exists(path));
        Assert.Contains("gui bootstrap log probe", File.ReadAllText(path));
    }
}

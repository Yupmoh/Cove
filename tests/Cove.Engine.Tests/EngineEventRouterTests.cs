using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Cove.Engine.Daemon;
using Cove.Protocol;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class EngineEventRouterTests
{
    [Fact]
    public async Task WorkspaceMutations_EmitMonotonicRevisionedEvents()
    {
        await using var harness = await RouterHarness.CreateAsync();

        harness.Router.PublishMutation("cove://commands/layout.mutate");
        var first = await harness.ReadWorkspaceChangedAsync();
        harness.Router.PublishMutation("cove://commands/nook.rename");
        var second = await harness.ReadWorkspaceChangedAsync();

        Assert.Equal(1, first.Revision);
        Assert.Equal("cove://commands/layout.mutate", first.Uri);
        Assert.Equal(2, second.Revision);
        Assert.Equal("cove://commands/nook.rename", second.Uri);
    }

    [Theory]
    [InlineData("cove://commands/layout.mutate", true)]
    [InlineData("cove://commands/nook.spawn", true)]
    [InlineData("cove://commands/nook.kill", true)]
    [InlineData("cove://commands/nook.rename", true)]
    [InlineData("cove://commands/nook.restart", true)]
    [InlineData("cove://commands/agent.launch", true)]
    [InlineData("cove://commands/nook.write", false)]
    [InlineData("cove://commands/nook.read", false)]
    [InlineData("cove://commands/nook.list", false)]
    [InlineData("cove://commands/layout.get", false)]
    [InlineData("cove://commands/layout.snapshot", false)]
    public void WorkspaceMutationClassification_MatchesVisibleContract(string uri, bool expected)
    {
        Assert.Equal(expected, EngineEventRouter.IsWorkspaceMutatingVerb(uri));
    }

    private sealed class RouterHarness : IAsyncDisposable
    {
        private readonly TcpListener _listener;
        private readonly TcpClient _senderClient;
        private readonly TcpClient _receiverClient;
        private readonly FrameConnection _sender;
        private readonly FrameConnection _receiver;

        private RouterHarness(
            TcpListener listener,
            TcpClient senderClient,
            TcpClient receiverClient,
            FrameConnection sender,
            FrameConnection receiver,
            EngineEventRouter router)
        {
            _listener = listener;
            _senderClient = senderClient;
            _receiverClient = receiverClient;
            _sender = sender;
            _receiver = receiver;
            Router = router;
        }

        public EngineEventRouter Router { get; }

        public static async Task<RouterHarness> CreateAsync()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var senderClient = new TcpClient();
            var connect = senderClient.ConnectAsync(
                IPAddress.Loopback,
                ((IPEndPoint)listener.LocalEndpoint).Port);
            var receiverClient = await listener.AcceptTcpClientAsync();
            await connect;
            var sender = new FrameConnection(receiverClient.GetStream());
            var receiver = new FrameConnection(senderClient.GetStream());
            var router = new EngineEventRouter(CancellationToken.None);
            router.RegisterGui(sender);
            return new RouterHarness(
                listener,
                senderClient,
                receiverClient,
                sender,
                receiver,
                router);
        }

        public async Task<WorkspaceChangedEvent> ReadWorkspaceChangedAsync()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            while (true)
            {
                var frame = await _receiver.ReadFrameAsync(cts.Token);
                Assert.NotNull(frame);
                if (frame.Value.Header.Type != FrameType.Event)
                    continue;
                var controlEvent = ControlCodec.DecodeEvent(frame.Value.Payload);
                if (controlEvent.Channel != "workspace.changed")
                    continue;
                return controlEvent.Payload.Deserialize(
                    CoveJsonContext.Default.WorkspaceChangedEvent)!;
            }
        }

        public async ValueTask DisposeAsync()
        {
            Router.UnregisterGui(_sender);
            await _receiver.DisposeAsync();
            await _sender.DisposeAsync();
            _receiverClient.Dispose();
            _senderClient.Dispose();
            _listener.Stop();
        }
    }
}

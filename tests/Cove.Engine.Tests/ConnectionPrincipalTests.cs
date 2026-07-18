using System.Text;
using System.Text.Json;
using Cove.Engine.Pty;
using Cove.Platform.Pty;
using Cove.Protocol;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class ConnectionPrincipalTests
{
    private static async Task<ControlResponse> RequestAsync(FrameConnection conn, string id, string uri, JsonElement? p, CancellationToken ct, string? callerNookId = null)
    {
        await conn.WriteFrameAsync(FrameType.Request, 0, ControlCodec.Encode(new ControlRequest(id, uri, p, null, callerNookId)), ct);
        while (true)
        {
            var frame = (await conn.ReadFrameAsync(ct))!.Value;
            if (frame.Header.Type != FrameType.Response)
                continue;
            var response = ControlCodec.DecodeResponse(frame.Payload);
            if (response.Id == id)
                return response;
        }
    }

    private static async Task<(FrameConnection conn, ControlResponse hello)> ConnectWithHelloAsync(DaemonTestHarness harness, string clientKind, string? nookId, string? nookToken, CancellationToken ct)
    {
        var stream = await harness.Endpoint.ConnectAsync(5000, ct);
        var conn = new FrameConnection(stream);
        var hp = JsonSerializer.SerializeToElement(
            new HelloParams(ProtocolConstants.SemanticProtocolVersion, clientKind, "0.1.0", "dev", nookId, nookToken),
            CoveJsonContext.Default.HelloParams);
        await conn.WriteFrameAsync(FrameType.Request, 0, ControlCodec.Encode(new ControlRequest("h", "cove://sys/hello", hp)), ct);
        var frame = (await conn.ReadFrameAsync(ct))!.Value;
        return (conn, ControlCodec.DecodeResponse(frame.Payload));
    }

    private static async Task<string> SpawnNookAsync(FrameConnection control, string script, CancellationToken ct)
    {
        var spawnParams = JsonSerializer.SerializeToElement(
            new SpawnParams("/bin/sh", ["-c", script], null, null, 80, 24),
            CoveJsonContext.Default.SpawnParams);
        var spawned = await RequestAsync(control, "spawn-" + Guid.NewGuid().ToString("N")[..8], "cove://commands/nook.spawn", spawnParams, ct);
        Assert.True(spawned.Ok, spawned.Error?.Message);
        return spawned.Data!.Value.Deserialize(CoveJsonContext.Default.NookInfo)!.NookId;
    }

    private static async Task<(string nookId, string token)> SpawnTokenEchoNookAsync(FrameConnection control, CancellationToken ct)
    {
        var nookId = await SpawnNookAsync(control, "printf 'TOK<%s>END' \"$COVE_NOOK_TOKEN\"; sleep 8", ct);
        var text = "";
        for (var i = 0; i < 100; i++)
        {
            var readParams = JsonSerializer.SerializeToElement(new NookReadParams(nookId, 0, 65536), CoveJsonContext.Default.NookReadParams);
            var read = await RequestAsync(control, "read" + i, "cove://commands/nook.read", readParams, ct);
            Assert.True(read.Ok, read.Error?.Message);
            var result = read.Data!.Value.Deserialize(CoveJsonContext.Default.NookReadResult)!;
            text = Encoding.UTF8.GetString(Convert.FromBase64String(result.DataBase64));
            if (text.Contains("END"))
                break;
            await Task.Delay(50, ct);
        }
        var start = text.IndexOf("TOK<", StringComparison.Ordinal);
        var end = text.IndexOf(">END", StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start, $"token marker missing in nook output: {text}");
        var token = text[(start + 4)..end];
        Assert.Equal(64, token.Length);
        Assert.True(token.All(Uri.IsHexDigit), $"token is not opaque hex: {token}");
        return (nookId, token);
    }

    [Fact]
    public async Task AuthenticatedNookConnection_CannotMutateScopes()
    {
        if (OperatingSystem.IsWindows()) return;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var ct = cts.Token;
        await using var harness = await DaemonTestHarness.StartAsync();
        await using var control = await harness.ConnectAsync("gui");
        var (nookId, token) = await SpawnTokenEchoNookAsync(control, ct);

        var (conn, hello) = await ConnectWithHelloAsync(harness, "cli", nookId, token, ct);
        await using var _ = conn;
        Assert.True(hello.Ok, hello.Error?.Message);

        var setParams = JsonSerializer.SerializeToElement(new NookScopeSetParams(nookId, "all"), CoveJsonContext.Default.NookScopeSetParams);
        var denied = await RequestAsync(conn, "set", "cove://commands/nook.scope.set", setParams, ct);
        Assert.False(denied.Ok);
        Assert.Equal("access_denied", denied.Error?.Code);
    }

    [Fact]
    public async Task AuthenticatedNookConnection_CannotForgeCallerIdentity()
    {
        if (OperatingSystem.IsWindows()) return;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var ct = cts.Token;
        await using var harness = await DaemonTestHarness.StartAsync();
        await using var control = await harness.ConnectAsync("gui");
        var (nookId, token) = await SpawnTokenEchoNookAsync(control, ct);

        var (conn, hello) = await ConnectWithHelloAsync(harness, "cli", nookId, token, ct);
        await using var _ = conn;
        Assert.True(hello.Ok, hello.Error?.Message);

        var setParams = JsonSerializer.SerializeToElement(new NookScopeSetParams(nookId, "all"), CoveJsonContext.Default.NookScopeSetParams);
        var forged = await RequestAsync(conn, "forge", "cove://commands/nook.scope.set", setParams, ct, callerNookId: "nook-somebody-else");
        Assert.False(forged.Ok);
        Assert.Equal("access_denied", forged.Error?.Code);
    }

    [Fact]
    public async Task AuthenticatedSameTabNook_CannotTargetOtherNooks()
    {
        if (OperatingSystem.IsWindows()) return;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var ct = cts.Token;
        await using var harness = await DaemonTestHarness.StartAsync();
        await using var control = await harness.ConnectAsync("gui");
        var (nookA, token) = await SpawnTokenEchoNookAsync(control, ct);
        var nookB = await SpawnNookAsync(control, "sleep 8", ct);

        var setParams = JsonSerializer.SerializeToElement(new NookScopeSetParams(nookA, "same-tab"), CoveJsonContext.Default.NookScopeSetParams);
        var scoped = await RequestAsync(control, "scope", "cove://commands/nook.scope.set", setParams, ct);
        Assert.True(scoped.Ok, scoped.Error?.Message);

        var (conn, hello) = await ConnectWithHelloAsync(harness, "cli", nookA, token, ct);
        await using var _ = conn;
        Assert.True(hello.Ok, hello.Error?.Message);

        var renameParams = JsonSerializer.SerializeToElement(new NookRenameParams(nookB, "hijacked"), CoveJsonContext.Default.NookRenameParams);
        var denied = await RequestAsync(conn, "rename", "cove://commands/nook.rename", renameParams, ct);
        Assert.False(denied.Ok);
        Assert.Equal("access_denied", denied.Error?.Code);

        var selfRename = JsonSerializer.SerializeToElement(new NookRenameParams(nookA, "self"), CoveJsonContext.Default.NookRenameParams);
        var allowed = await RequestAsync(conn, "self", "cove://commands/nook.rename", selfRename, ct);
        Assert.True(allowed.Ok, allowed.Error?.Message);
    }

    [Fact]
    public async Task Hello_WithWrongToken_IsRejected()
    {
        if (OperatingSystem.IsWindows()) return;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var ct = cts.Token;
        await using var harness = await DaemonTestHarness.StartAsync();
        await using var control = await harness.ConnectAsync("gui");
        var (nookId, _) = await SpawnTokenEchoNookAsync(control, ct);

        var (conn, hello) = await ConnectWithHelloAsync(harness, "cli", nookId, new string('F', 64), ct);
        await using var _ = conn;
        Assert.False(hello.Ok);
        Assert.Equal("nook_auth_failed", hello.Error?.Code);
    }

    [Fact]
    public async Task Hello_WithUnknownNookId_FallsBackToAnonymous_WithoutScopeAuthority()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var ct = cts.Token;
        await using var harness = await DaemonTestHarness.StartAsync();

        var (conn, hello) = await ConnectWithHelloAsync(harness, "cli", "nook-does-not-exist", new string('A', 64), ct);
        await using var _ = conn;
        Assert.True(hello.Ok, hello.Error?.Message);

        var setParams = JsonSerializer.SerializeToElement(new NookScopeSetParams("nook-does-not-exist", "all"), CoveJsonContext.Default.NookScopeSetParams);
        var denied = await RequestAsync(conn, "set", "cove://commands/nook.scope.set", setParams, ct);
        Assert.False(denied.Ok);
        Assert.Equal("access_denied", denied.Error?.Code);
    }

    [Fact]
    public async Task AnonymousCli_CannotMutateScopes_ButGuiCan()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var ct = cts.Token;
        await using var harness = await DaemonTestHarness.StartAsync();
        await using var gui = await harness.ConnectAsync("gui");
        await using var cli = await harness.ConnectAsync("cli");

        var setParams = JsonSerializer.SerializeToElement(new NookScopeSetParams("nook-x", "same-bay"), CoveJsonContext.Default.NookScopeSetParams);
        var denied = await RequestAsync(cli, "set", "cove://commands/nook.scope.set", setParams, ct);
        Assert.False(denied.Ok);
        Assert.Equal("access_denied", denied.Error?.Code);

        var allowed = await RequestAsync(gui, "set", "cove://commands/nook.scope.set", setParams, ct);
        Assert.True(allowed.Ok, allowed.Error?.Message);
    }

    [Fact]
    public async Task AnonymousConnection_ClaimOfTokenedNook_IsStripped()
    {
        if (OperatingSystem.IsWindows()) return;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var ct = cts.Token;
        await using var harness = await DaemonTestHarness.StartAsync();
        await using var control = await harness.ConnectAsync("gui");
        var (nookA, _) = await SpawnTokenEchoNookAsync(control, ct);
        var nookB = await SpawnNookAsync(control, "sleep 8", ct);

        var setParams = JsonSerializer.SerializeToElement(new NookScopeSetParams(nookA, "same-tab"), CoveJsonContext.Default.NookScopeSetParams);
        var scoped = await RequestAsync(control, "scope", "cove://commands/nook.scope.set", setParams, ct);
        Assert.True(scoped.Ok, scoped.Error?.Message);

        var (conn, hello) = await ConnectWithHelloAsync(harness, "cli", null, null, ct);
        await using var _ = conn;
        Assert.True(hello.Ok, hello.Error?.Message);

        var renameParams = JsonSerializer.SerializeToElement(new NookRenameParams(nookB, "renamed-anon"), CoveJsonContext.Default.NookRenameParams);
        var response = await RequestAsync(conn, "rename", "cove://commands/nook.rename", renameParams, ct, callerNookId: nookA);
        Assert.True(response.Ok, response.Error?.Message);
    }

    [Fact]
    public void HandoffExportAdopt_PreservesNookToken()
    {
        if (OperatingSystem.IsWindows()) return;
        var dataDir = Directory.CreateTempSubdirectory().FullName;
        var spawnEnv = new SpawnEnvironment("/usr/bin:/bin", dataDir, "/bin/echo", "default");
        using var source = new NookRegistry(PtyHostFactory.Create(NullLogger.Instance), NullLogger.Instance, spawnEnv);
        using var successor = new NookRegistry(PtyHostFactory.Create(NullLogger.Instance), NullLogger.Instance, spawnEnv);

        var info = source.Spawn(new SpawnParams("/bin/sh", ["-c", "printf 'TOK<%s>END' \"$COVE_NOOK_TOKEN\"; sleep 8"], null, null, 80, 24));
        var token = "";
        for (var i = 0; i < 100; i++)
        {
            var text = Encoding.UTF8.GetString(source.Read(info.NookId, 0, 65536));
            var start = text.IndexOf("TOK<", StringComparison.Ordinal);
            var end = text.IndexOf(">END", StringComparison.Ordinal);
            if (start >= 0 && end > start)
            {
                token = text[(start + 4)..end];
                break;
            }
            Thread.Sleep(50);
        }
        Assert.Equal(64, token.Length);
        Assert.Equal(NookAuthResult.Bound, source.Authenticate(info.NookId, token));
        Assert.Equal(NookAuthResult.Rejected, source.Authenticate(info.NookId, new string('F', 64)));
        Assert.Equal(NookAuthResult.Unknown, source.Authenticate("nook-missing", token));

        var items = source.ExportForHandoff();
        var item = Assert.Single(items);
        var adopted = successor.Adopt(item.Record, item.MasterFd, item.RingTail);
        Assert.NotNull(adopted);
        Assert.Equal(NookAuthResult.Bound, successor.Authenticate(info.NookId, token));
        Assert.Equal(NookAuthResult.Rejected, successor.Authenticate(info.NookId, null));
        successor.Stop(info.NookId);
    }
}

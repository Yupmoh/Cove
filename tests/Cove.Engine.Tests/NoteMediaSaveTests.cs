using System.Text.Json;
using Cove.Engine;
using Cove.Platform.Ipc;
using Cove.Protocol;
using Xunit;
using Cove.Testing;

namespace Cove.Engine.Tests;

public sealed class NoteMediaSaveTests
{
    private static JsonElement P(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private static async Task<ControlResponse> SendAsync(FrameConnection ctl, string id, string uri, JsonElement? p, CancellationToken ct)
    {
        await ctl.WriteFrameAsync(FrameType.Request, 0, ControlCodec.Encode(new ControlRequest(id, uri, p)), ct);
        while (true)
        {
            Frame f = (await ctl.ReadFrameAsync(ct))!.Value;
            if (f.Header.Type != FrameType.Response) continue;
            ControlResponse r = ControlCodec.DecodeResponse(f.Payload);
            if (r.Id == id) return r;
        }
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    public async Task MediaSave_PersistsImageBytes_OverSocket()
    {
        using var cts = new CancellationTokenSource(System.TimeSpan.FromSeconds(60));
        CancellationToken ct = cts.Token;

        await using var h = await DaemonTestHarness.StartAsync();
        await using FrameConnection ctl = await h.ConnectAsync("cli");

        var createResp = await SendAsync(ctl, "c1", "cove://commands/note.create", P("""{"title":"Test","bayId":"ws1","source":"test","content":"","kind":"markdown"}"""), ct);
        var noteId = createResp.Data!.Value.GetProperty("id").GetString()!;

        var imageBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        var base64 = System.Convert.ToBase64String(imageBytes);

        var mediaResp = await SendAsync(ctl, "m1", "cove://commands/note.media.save", P($"{{\"bayId\":\"ws1\",\"id\":\"{noteId}\",\"fileName\":\"test.png\",\"base64Data\":\"{base64}\"}}"), ct);
        Assert.True(mediaResp.Ok, mediaResp.Error?.Code);
        var mediaPath = mediaResp.Data!.Value.GetProperty("mediaPath").GetString()!;
        Assert.Contains(noteId, mediaPath);
        Assert.Contains("test.png", mediaPath);

        var fullPath = System.IO.Path.Combine(h.DataDir, "notes", mediaPath);
        Assert.True(System.IO.File.Exists(fullPath));
        var savedBytes = await System.IO.File.ReadAllBytesAsync(fullPath);
        Assert.Equal(imageBytes, savedBytes);
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    public async Task MediaSave_DataUrlPrefix_ReturnsInvalidDataError()
    {
        using var cts = new CancellationTokenSource(System.TimeSpan.FromSeconds(60));
        CancellationToken ct = cts.Token;

        await using var h = await DaemonTestHarness.StartAsync();
        await using FrameConnection ctl = await h.ConnectAsync("cli");

        var createResp = await SendAsync(ctl, "c1", "cove://commands/note.create", P("""{"title":"Test","bayId":"ws1","source":"test","content":"","kind":"markdown"}"""), ct);
        var noteId = createResp.Data!.Value.GetProperty("id").GetString()!;

        var dataUrl = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAAB";
        var mediaResp = await SendAsync(ctl, "m1", "cove://commands/note.media.save", P($"{{\"bayId\":\"ws1\",\"id\":\"{noteId}\",\"fileName\":\"test.png\",\"base64Data\":\"{dataUrl}\"}}"), ct);
        Assert.False(mediaResp.Ok);
        Assert.Equal("invalid_data", mediaResp.Error!.Code);
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    public async Task MediaSave_MalformedBase64_ReturnsInvalidDataError()
    {
        using var cts = new CancellationTokenSource(System.TimeSpan.FromSeconds(60));
        CancellationToken ct = cts.Token;

        await using var h = await DaemonTestHarness.StartAsync();
        await using FrameConnection ctl = await h.ConnectAsync("cli");

        var createResp = await SendAsync(ctl, "c1", "cove://commands/note.create", P("""{"title":"Test","bayId":"ws1","source":"test","content":"","kind":"markdown"}"""), ct);
        var noteId = createResp.Data!.Value.GetProperty("id").GetString()!;

        var mediaResp = await SendAsync(ctl, "m1", "cove://commands/note.media.save", P($"{{\"bayId\":\"ws1\",\"id\":\"{noteId}\",\"fileName\":\"test.png\",\"base64Data\":\"!!!not-base64!!!\"}}"), ct);
        Assert.False(mediaResp.Ok);
        Assert.Equal("invalid_data", mediaResp.Error!.Code);
    }
}

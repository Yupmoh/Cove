using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Cove.Adapters;
using Cove.Engine;
using Cove.Engine.Sessions;
using Cove.Protocol;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class SessionRecentCommandTests
{
    private static string CopyFixture(string name)
    {
        var root = Path.Combine(Path.GetTempPath(), "cove-recent-cmd-" + Guid.NewGuid().ToString("N"));
        var dir = Path.Combine(root, name);
        Directory.CreateDirectory(dir);
        var src = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..",
            "tests", "fixtures", "adapters", name);
        foreach (var f in Directory.GetFiles(src))
            File.Copy(f, Path.Combine(dir, Path.GetFileName(f)), true);
        return root;
    }

    private static void WriteListScript(string adapterDir, string body)
    {
        var path = Path.Combine(adapterDir, "list_recent_sessions.sh");
        File.WriteAllText(path, "#!/usr/bin/env bash\nset -euo pipefail\n" + body);
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    private static Task<ControlResponse?> Route(string adaptersRoot, SessionRecentParams p)
    {
        var manifests = new AdapterManifestStore(adaptersRoot);
        var svc = new SessionService(new MethodRunner());
        var el = JsonSerializer.SerializeToElement(p, SessionRecentJsonContext.Default.SessionRecentParams);
        var request = new ControlRequest("1", "cove://commands/session.recent", el);
        return EngineCommandRouter.RouteAsync(request, manifestStore: manifests, sessionService: svc);
    }

    [Fact]
    public async Task Recent_ReturnsAdapterSessionIdsAndLabels_NewestFirst()
    {
        if (OperatingSystem.IsWindows()) return;
        var root = CopyFixture("test-v2");
        try
        {
            WriteListScript(Path.Combine(root, "test-v2"), """
            CWD="$1"
            cat <<EOF
            {"sessions":[
              {"id":"real-s1","name":"Fix the router","cwd":"$CWD","lastActive":"2024-01-01T00:00:00Z"},
              {"id":"real-s2","name":"Add tests","cwd":"$CWD","lastActive":"2024-06-01T00:00:00Z"}
            ]}
            EOF
            """);

            var resp = await Route(root, new SessionRecentParams("test-v2", null, "/repo/work"));

            Assert.True(resp!.Ok);
            var sessions = resp.Data!.Value.GetProperty("sessions");
            Assert.Equal(2, sessions.GetArrayLength());
            Assert.Equal("real-s2", sessions[0].GetProperty("sessionId").GetString());
            Assert.Equal("Add tests", sessions[0].GetProperty("label").GetString());
            Assert.Equal("real-s1", sessions[1].GetProperty("sessionId").GetString());
            Assert.Equal("Fix the router", sessions[1].GetProperty("label").GetString());
            Assert.Equal("/repo/work", sessions[0].GetProperty("cwd").GetString());
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }

    [Fact]
    public async Task Recent_FailingAdapter_DegradesToEmpty_NotError()
    {
        if (OperatingSystem.IsWindows()) return;
        var root = CopyFixture("test-v2");
        try
        {
            WriteListScript(Path.Combine(root, "test-v2"), "exit 3\n");

            var resp = await Route(root, new SessionRecentParams("test-v2", null, "/repo/work"));

            Assert.True(resp!.Ok);
            Assert.Equal(0, resp.Data!.Value.GetProperty("sessions").GetArrayLength());
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }

    [Fact]
    public async Task Recent_NoSessionService_IsNotReady()
    {
        var resp = await EngineCommandRouter.RouteAsync(new ControlRequest("1", "cove://commands/session.recent", null));
        Assert.False(resp!.Ok);
        Assert.Equal("not_ready", resp.Error!.Code);
    }
}

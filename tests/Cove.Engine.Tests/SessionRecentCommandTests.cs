using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Cove.Adapters;
using Cove.Engine;
using Cove.Engine.Sessions;
using Cove.Protocol;
using Xunit;
using Cove.Testing;

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

    private static Task<ControlResponse?> Route(string adaptersRoot, SessionRecentParams p, string? baysDir = null)
    {
        var manifests = new AdapterManifestStore(adaptersRoot);
        var svc = new SessionService(new MethodRunner());
        var el = JsonSerializer.SerializeToElement(p, SessionRecentJsonContext.Default.SessionRecentParams);
        var request = new ControlRequest("1", "cove://commands/session.recent", el);
        return EngineCommandRouter.RouteAsync(request, manifestStore: manifests, sessionService: svc, baysDir: baysDir);
    }

    private static void WriteNookRecord(string baysDir, string bayId, string nookId, string title, string sessionId)
    {
        var wsDir = Path.Combine(baysDir, bayId);
        var desc = new Cove.Persistence.NookDescriptor(nookId, "", Array.Empty<string>(), "/repo/work", title, "test-v2", null, sessionId, false);
        Cove.Persistence.AtomicJsonStore.Write(
            Path.Combine(wsDir, "nooks", nookId, "session.json"),
            desc,
            Cove.Persistence.CoveJsonContext.Default.NookDescriptor);
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    public async Task Recent_ReturnsAdapterSessionIdsAndLabels_NewestFirst()
    {
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
        finally { Cove.Testing.TestDirectory.Delete(root); }
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    public async Task Recent_FailingAdapter_DegradesToEmpty_NotError()
    {
        var root = CopyFixture("test-v2");
        try
        {
            WriteListScript(Path.Combine(root, "test-v2"), "exit 3\n");

            var resp = await Route(root, new SessionRecentParams("test-v2", null, "/repo/work"));

            Assert.True(resp!.Ok);
            Assert.Equal(0, resp.Data!.Value.GetProperty("sessions").GetArrayLength());
        }
        finally { Cove.Testing.TestDirectory.Delete(root); }
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    public async Task Recent_PrefersNookTitleOverAdapterLabel()
    {
        var root = CopyFixture("test-v2");
        var baysDir = Path.Combine(Path.GetTempPath(), "cove-recent-bays-" + Guid.NewGuid().ToString("N"));
        try
        {
            WriteListScript(Path.Combine(root, "test-v2"), """
            CWD="$1"
            cat <<EOF
            {"sessions":[
              {"id":"real-s1","name":"claude summary","cwd":"$CWD","lastActive":"2024-01-01T00:00:00Z"}
            ]}
            EOF
            """);
            WriteNookRecord(baysDir, "bay-a", "n1", "cove-session", "real-s1");

            var resp = await Route(root, new SessionRecentParams("test-v2", null, "/repo/work"), baysDir);

            Assert.True(resp!.Ok);
            var sessions = resp.Data!.Value.GetProperty("sessions");
            Assert.Equal(1, sessions.GetArrayLength());
            Assert.Equal("cove-session", sessions[0].GetProperty("label").GetString());
        }
        finally { Cove.Testing.TestDirectory.Delete(root); Cove.Testing.TestDirectory.Delete(baysDir); }
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    public async Task Recent_KeepsDistinctSessionsWithIdenticalLabels()
    {
        var root = CopyFixture("test-v2");
        var baysDir = Path.Combine(Path.GetTempPath(), "cove-recent-bays-" + Guid.NewGuid().ToString("N"));
        try
        {
            WriteListScript(Path.Combine(root, "test-v2"), """
            CWD="$1"
            cat <<EOF
            {"sessions":[
              {"id":"real-old","name":"whatever old","cwd":"$CWD","lastActive":"2024-01-01T00:00:00Z"},
              {"id":"real-new","name":"whatever new","cwd":"$CWD","lastActive":"2024-06-01T00:00:00Z"}
            ]}
            EOF
            """);
            WriteNookRecord(baysDir, "bay-a", "n1", "cove-session", "real-old");
            WriteNookRecord(baysDir, "bay-a", "n2", "cove-session", "real-new");

            var resp = await Route(root, new SessionRecentParams("test-v2", null, "/repo/work"), baysDir);

            Assert.True(resp!.Ok);
            var sessions = resp.Data!.Value.GetProperty("sessions");
            Assert.Equal(2, sessions.GetArrayLength());
            Assert.Equal("real-new", sessions[0].GetProperty("sessionId").GetString());
            Assert.Equal("real-old", sessions[1].GetProperty("sessionId").GetString());
            Assert.Equal("cove-session", sessions[0].GetProperty("label").GetString());
            Assert.Equal("cove-session", sessions[1].GetProperty("label").GetString());
        }
        finally { Cove.Testing.TestDirectory.Delete(root); Cove.Testing.TestDirectory.Delete(baysDir); }
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    public async Task Recent_KeepsDistinctLabels()
    {
        var root = CopyFixture("test-v2");
        var baysDir = Path.Combine(Path.GetTempPath(), "cove-recent-bays-" + Guid.NewGuid().ToString("N"));
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
            WriteNookRecord(baysDir, "bay-a", "n1", "cove-session", "real-s2");

            var resp = await Route(root, new SessionRecentParams("test-v2", null, "/repo/work"), baysDir);

            Assert.True(resp!.Ok);
            var sessions = resp.Data!.Value.GetProperty("sessions");
            Assert.Equal(2, sessions.GetArrayLength());
            Assert.Equal("cove-session", sessions[0].GetProperty("label").GetString());
            Assert.Equal("Fix the router", sessions[1].GetProperty("label").GetString());
        }
        finally { Cove.Testing.TestDirectory.Delete(root); Cove.Testing.TestDirectory.Delete(baysDir); }
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    public async Task Recent_ZeroLimitReturnsEveryDiscoveredSession()
    {
        var root = CopyFixture("test-v2");
        try
        {
            var rows = string.Join(",", Enumerable.Range(1, 25).Select(i =>
                $"{{\"id\":\"session-{i}\",\"name\":\"Session {i}\",\"cwd\":\"/repo/work\",\"lastActive\":\"2024-01-01T00:00:00Z\"}}"));
            WriteListScript(Path.Combine(root, "test-v2"), $"printf '%s\\n' '{{\"sessions\":[{rows}]}}'\n");

            var resp = await Route(root, new SessionRecentParams("test-v2", 0, "/repo/work"));

            Assert.True(resp!.Ok);
            Assert.Equal(25, resp.Data!.Value.GetProperty("sessions").GetArrayLength());
        }
        finally { Cove.Testing.TestDirectory.Delete(root); }
    }

    [Fact]
    public async Task Recent_NoSessionService_IsNotReady()
    {
        var resp = await EngineCommandRouter.RouteAsync(new ControlRequest("1", "cove://commands/session.recent", null));
        Assert.False(resp!.Ok);
        Assert.Equal("not_ready", resp.Error!.Code);
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    public async Task Recent_SkipsAdaptersThatDeclareNoSessionListing()
    {
        var root = CopyFixture("test-v2");
        try
        {
            WriteListScript(Path.Combine(root, "test-v2"),
                "echo '{\"sessions\":[{\"id\":\"s1\",\"name\":\"real\",\"cwd\":\"/repo/work\",\"lastActive\":\"2026-07-16T00:00:00Z\"}]}'\n");

            var silent = Path.Combine(root, "methodless");
            Directory.CreateDirectory(silent);
            File.WriteAllText(Path.Combine(silent, "adapter.json"), """
                {"sdkVersion":2,"name":"methodless","displayName":"Methodless","description":"t","accent":"#fff","binary":"methodless","version":"1.0.0","methods":{}}
                """);
            WriteListScript(silent,
                "echo '{\"sessions\":[{\"id\":\"ghost\",\"name\":\"ghost\",\"cwd\":\"/repo/work\",\"lastActive\":\"2026-07-16T00:00:00Z\"}]}'\n");

            var resp = await Route(root, new SessionRecentParams(null, null, "/repo/work"));

            Assert.True(resp!.Ok);
            var sessions = resp.Data!.Value.GetProperty("sessions");
            var ids = new List<string>();
            foreach (var s in sessions.EnumerateArray())
                ids.Add(s.GetProperty("sessionId").GetString() ?? "");
            Assert.Contains("s1", ids);
            Assert.DoesNotContain("ghost", ids);
        }
        finally { Cove.Testing.TestDirectory.Delete(root); }
    }
}

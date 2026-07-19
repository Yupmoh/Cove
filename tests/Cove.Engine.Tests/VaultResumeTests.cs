using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Cove.Adapters;
using Cove.Engine;
using Cove.Engine.Knowledge;
using Cove.Engine.Launch;
using Cove.Engine.Restart;
using Cove.Generated;
using Cove.Protocol;
using Xunit;
using Cove.Testing;

namespace Cove.Engine.Tests;

public sealed class VaultResumeTests
{
    private static string WriteFixtures(params string[] names)
    {
        var root = Path.Combine(Path.GetTempPath(), "cove-vaultresume-" + Guid.NewGuid().ToString("N"));
        foreach (var name in names)
        {
            var dir = Path.Combine(root, name);
            Directory.CreateDirectory(dir);
            var src = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..",
                "tests", "fixtures", "adapters", name);
            foreach (var f in Directory.GetFiles(src))
                File.Copy(f, Path.Combine(dir, Path.GetFileName(f)), true);
        }
        return root;
    }

    private static EngineDispatchContext Context(string adaptersRoot, VaultResumeParams p)
    {
        var manifestStore = new AdapterManifestStore(adaptersRoot);
        var methodRunner = new MethodRunner();
        var resumeService = new AgentResumeService(new AdapterResumeProtocol(manifestStore, methodRunner));
        var launcher = new LaunchOrchestrator(manifestStore, methodRunner, new BinaryDiscoveryService(), null, resumeService);
        var launchProfiles = new LaunchProfileStore(Path.Combine(adaptersRoot, "profiles"));
        var el = JsonSerializer.SerializeToElement(p, CoveJsonContext.Default.VaultResumeParams);
        var request = new ControlRequest("1", "cove://commands/vault.resume", el);
        return new EngineDispatchContext(request, manifestStore: manifestStore, launcher: launcher, launchProfiles: launchProfiles);
    }

    private static VaultResumeResult Deserialize(ControlResponse resp)
        => resp.Data!.Value.Deserialize(CoveJsonContext.Default.VaultResumeResult)!;

    [PlatformFact(TestOperatingSystem.Unix)]
    public async Task VaultResume_AdapterWithResumeMethod_ReturnsResumeArgv()
    {
        var root = WriteFixtures("test-v2");
        try
        {
            var ctx = Context(root, new VaultResumeParams("test-v2", "sess-123", "/tmp/work"));
            var resp = await KnowledgeCommands.VaultResume(ctx);

            Assert.True(resp.Ok);
            var result = Deserialize(resp);
            Assert.True(result.Ok);
            Assert.Equal("none", result.Fallback);
            Assert.Equal("test-v2", result.Adapter);
            Assert.Equal(new[] { "test-v2", "resume", "sess-123" }, result.Command);
            Assert.Equal("/tmp/work", result.Cwd);
        }
        finally { Cove.Testing.TestDirectory.Delete(root); }
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    public async Task VaultResume_Yolo_AddsSkipPermissionsFlag()
    {
        var root = WriteFixtures("test-v2");
        try
        {
            var ctx = Context(root, new VaultResumeParams("test-v2", "sess-123", "/tmp/work", Yolo: true));
            var resp = await KnowledgeCommands.VaultResume(ctx);

            Assert.True(resp.Ok);
            var result = Deserialize(resp);
            Assert.True(result.Ok);
            Assert.Equal("none", result.Fallback);
            Assert.Equal(new[] { "test-v2", "resume", "sess-123", "--dangerously-skip-permissions" }, result.Command);
        }
        finally { Cove.Testing.TestDirectory.Delete(root); }
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    public async Task VaultResume_NoYolo_OmitsSkipPermissionsFlag()
    {
        var root = WriteFixtures("test-v2");
        try
        {
            var ctx = Context(root, new VaultResumeParams("test-v2", "sess-123", "/tmp/work", Yolo: false));
            var resp = await KnowledgeCommands.VaultResume(ctx);

            Assert.True(resp.Ok);
            var result = Deserialize(resp);
            Assert.Equal(new[] { "test-v2", "resume", "sess-123" }, result.Command);
        }
        finally { Cove.Testing.TestDirectory.Delete(root); }
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    public async Task VaultResume_AdapterWithoutResumeMethod_FallsBackToFreshLaunch()
    {
        var root = WriteFixtures("test-v1");
        try
        {
            var ctx = Context(root, new VaultResumeParams("test-v1", "sess-9", "/tmp/work"));
            var resp = await KnowledgeCommands.VaultResume(ctx);

            Assert.True(resp.Ok);
            var result = Deserialize(resp);
            Assert.True(result.Ok);
            Assert.Equal("fresh", result.Fallback);
            Assert.Equal("test-v1", result.Adapter);
            Assert.Equal("test-v1", result.Command[0]);
            Assert.Equal("/tmp/work", result.Cwd);
            Assert.NotNull(result.Error);
        }
        finally { Cove.Testing.TestDirectory.Delete(root); }
    }

    [Fact]
    public async Task VaultResume_UnknownAdapter_FailsCleanly()
    {
        var root = WriteFixtures("test-v2");
        try
        {
            var ctx = Context(root, new VaultResumeParams("never-installed", "sess-1", "/tmp/work"));
            var resp = await KnowledgeCommands.VaultResume(ctx);

            Assert.False(resp.Ok);
            Assert.Equal("not_found", resp.Error!.Code);
        }
        finally { Cove.Testing.TestDirectory.Delete(root); }
    }
}
